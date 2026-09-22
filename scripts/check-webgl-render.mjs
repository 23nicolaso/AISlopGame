// Renders the built WebGL bundle in headless Chrome and checks the pixels, because a bundle that loads and validates
// can still draw the wrong picture: the first itch.io build of Orbit Snake lost its main light and shipped a flat
// navy planet with an invisible ship, and nothing upstream noticed. Serves the bundle over HTTP, drives Chrome over
// the DevTools protocol (no npm dependencies), waits for the template's ORBIT_WEBGL_READY console line plus a few
// frames, screenshots, and asserts that the start screen shows lit land (green pixels) and is not mostly black.
//
//   node scripts/check-webgl-render.mjs [Builds/ORBIT-web] [--keep out.png]
//   CHROME=/path/to/chrome overrides the browser; TIMEOUT_MS (default 90000).
import { spawn, execFileSync } from 'node:child_process';
import { createServer } from 'node:http';
import { readFile, writeFile, stat, mkdtemp, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import assert from 'node:assert/strict';
import { inflateSync } from 'node:zlib';

const root=path.resolve(process.argv[2]&&!process.argv[2].startsWith('--')?process.argv[2]:'Builds/ORBIT-web');
const keepAt=process.argv.includes('--keep')?process.argv[process.argv.indexOf('--keep')+1]:null;
const timeoutMs=Number(process.env.TIMEOUT_MS||90000);
const chrome=process.env.CHROME||['/Applications/Google Chrome.app/Contents/MacOS/Google Chrome','/usr/bin/google-chrome','/usr/bin/chromium-browser','/usr/bin/chromium'].find(p=>{try{return execFileSync('test',['-x',p]),true;}catch{return false;}});
assert(chrome,'No Chrome found; set CHROME=/path/to/chrome');
const types={'.html':'text/html','.js':'application/javascript','.wasm':'application/wasm','.data':'application/octet-stream','.unityweb':'application/octet-stream','.json':'application/json','.png':'image/png','.ico':'image/x-icon','.css':'text/css'};

// Static server: itch.io serves .unityweb without Content-Encoding, so this one does too (the decompression fallback must cope).
const server=createServer(async(req,res)=>{try{
  const file=path.join(root,decodeURIComponent(new URL(req.url,'http://x').pathname).replace(/\/$/,'/index.html'));
  assert(!path.relative(root,file).startsWith('..'));
  const body=await readFile(file);res.writeHead(200,{'content-type':types[path.extname(file)]||'application/octet-stream','content-length':body.length});res.end(body);
}catch{res.writeHead(404);res.end();}});
await new Promise(r=>server.listen(0,'127.0.0.1',r));
const url=`http://127.0.0.1:${server.address().port}/`;

const profile=await mkdtemp(path.join(tmpdir(),'orbit-render-'));
const port=9222+Math.floor(Math.random()*1000);
const browser=spawn(chrome,['--headless=new','--hide-scrollbars','--window-size=1280,720','--no-first-run','--no-default-browser-check',`--user-data-dir=${profile}`,`--remote-debugging-port=${port}`,'--autoplay-policy=no-user-gesture-required',`--use-angle=${process.env.ANGLE||'default'}`,'--enable-unsafe-swiftshader',...(process.env.CI?['--no-sandbox']:[]),'about:blank'],{stdio:'ignore'});
const cleanup=async()=>{browser.kill();server.close();await rm(profile,{recursive:true,force:true}).catch(()=>{});};
process.on('exit',()=>browser.kill());
const deadline=Date.now()+timeoutMs;
async function target(){while(Date.now()<deadline){try{const list=await (await fetch(`http://127.0.0.1:${port}/json`)).json();const page=list.find(t=>t.type==='page');if(page)return page.webSocketDebuggerUrl;}catch{}await new Promise(r=>setTimeout(r,250));}throw new Error('Chrome did not expose a page target');}
const ws=new WebSocket(await target());
await new Promise((res,rej)=>{ws.onopen=res;ws.onerror=rej;});
let id=0;const pending=new Map();const events=[];
ws.onmessage=m=>{const msg=JSON.parse(m.data);if(msg.id&&pending.has(msg.id)){pending.get(msg.id)(msg);pending.delete(msg.id);}else events.push(msg);};
const send=(method,params={})=>new Promise(res=>{const n=++id;pending.set(n,res);ws.send(JSON.stringify({id:n,method,params}));});
const seen=(pred)=>events.some(pred);

try{
  await send('Runtime.enable');await send('Page.enable');await send('Log.enable');
  await send('Emulation.setDeviceMetricsOverride',{width:1280,height:720,deviceScaleFactor:1,mobile:false});
  await send('Page.navigate',{url});
  const ready=()=>seen(e=>e.method==='Runtime.consoleAPICalled'&&JSON.stringify(e.params.args).includes('ORBIT_WEBGL_READY'));
  const failed=()=>seen(e=>(e.method==='Runtime.consoleAPICalled'&&JSON.stringify(e.params.args).includes('ORBIT_WEBGL_LOAD_FAILED'))||e.method==='Runtime.exceptionThrown');
  while(!ready()){assert(!failed(),'The page reported a load failure: '+JSON.stringify(events.filter(e=>e.method!=='Runtime.consoleAPICalled'||JSON.stringify(e.params.args).includes('FAILED')).slice(-3)));assert(Date.now()<deadline,'Timed out waiting for ORBIT_WEBGL_READY');await new Promise(r=>setTimeout(r,250));}
  await new Promise(r=>setTimeout(r,3000)); // a few frames for the runtime scene build and the first draws
  const shot=await send('Page.captureScreenshot',{format:'png'});
  const png=Buffer.from(shot.result.data,'base64');
  if(keepAt)await writeFile(keepAt,png);
  const {width,height,pixels}=decodePng(png);
  let land=0,dark=0,total=width*height;
  for(let i=0;i<total;i++){const r=pixels[i*4],g=pixels[i*4+1],b=pixels[i*4+2];if(g>r+15&&g>b+15&&g>60)land++;if(r<20&&g<20&&b<20)dark++;}
  const landPct=100*land/total,darkPct=100*dark/total;
  console.log(`render: ${width}x${height}, lit land ${landPct.toFixed(2)}%, near-black ${darkPct.toFixed(1)}%`);
  assert(landPct>=1.5,'Too little lit land on the start screen: the planet is not lit (main light or Lit shader missing)');
  assert(darkPct<=75,'Frame is mostly black: the scene did not draw');
  console.log('WEBGL_RENDER_OK');
}finally{await cleanup();}

// Minimal PNG decoder (8-bit RGB/RGBA, non-interlaced), enough for a Chrome screenshot.
function decodePng(buf){
  const inflate=inflateSync;
  assert(buf.readUInt32BE(0)===0x89504e47,'not a PNG');
  let pos=8,width=0,height=0,colorType=0,idat=[];
  while(pos<buf.length){const len=buf.readUInt32BE(pos);const type=buf.toString('ascii',pos+4,pos+8);const data=buf.subarray(pos+8,pos+8+len);
    if(type==='IHDR'){width=data.readUInt32BE(0);height=data.readUInt32BE(4);assert(data[8]===8,'8-bit PNG expected');colorType=data[9];assert(data[12]===0,'interlaced PNG not supported');}
    else if(type==='IDAT')idat.push(data);else if(type==='IEND')break;pos+=12+len;}
  const bpp=colorType===6?4:3;const raw=inflate(Buffer.concat(idat));const stride=width*bpp;const out=Buffer.alloc(width*height*4);
  let prev=Buffer.alloc(stride);
  for(let y=0;y<height;y++){const filter=raw[y*(stride+1)];const line=Buffer.from(raw.subarray(y*(stride+1)+1,(y+1)*(stride+1)));
    for(let i=0;i<stride;i++){const a=i>=bpp?line[i-bpp]:0,b=prev[i],c=i>=bpp?prev[i-bpp]:0;let v=line[i];
      if(filter===1)v+=a;else if(filter===2)v+=b;else if(filter===3)v+=(a+b)>>1;else if(filter===4){const p=a+b-c,pa=Math.abs(p-a),pb=Math.abs(p-b),pc=Math.abs(p-c);v+=(pa<=pb&&pa<=pc)?a:(pb<=pc?b:c);}
      line[i]=v&255;}
    for(let x=0;x<width;x++){out[(y*width+x)*4]=line[x*bpp];out[(y*width+x)*4+1]=line[x*bpp+1];out[(y*width+x)*4+2]=line[x*bpp+2];out[(y*width+x)*4+3]=bpp===4?line[x*bpp+3]:255;}
    prev=line;}
  return {width,height,pixels:out};
}
