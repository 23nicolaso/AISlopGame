// Validates a built WebGL bundle against itch.io's limits before anything is uploaded (ported from unity-flappybird).
import { readdir, readFile, stat } from 'node:fs/promises';
import path from 'node:path';
import assert from 'node:assert/strict';

export async function validate(root) {
  const files=[];
  async function walk(dir){for(const item of await readdir(dir,{withFileTypes:true})){
    const file=path.join(dir,item.name);
    assert(!item.isSymbolicLink(),'No symlinks in published bundle');
    if(item.isDirectory())await walk(file);else files.push(file);
  }}
  await walk(root);
  const html=await readFile(path.join(root,'index.html'),'utf8');
  assert(!html.includes('{{{'),'Unity template must be expanded');
  assert(html.includes('matchWebGLToCanvasSize:true'),'Canvas must resize with iframe');
  assert(files.length<=1000,'itch.io maximum 1000 files');
  let total=0;
  for(const file of files){const size=(await stat(file)).size;total+=size;
    assert(size<=200*1024*1024,'itch.io maximum single file size');
    assert(path.relative(root,file).length<=240,'itch.io maximum path length');
  }
  assert(total<=500*1024*1024,'itch.io maximum bundle size');
  for(const field of ['dataUrl','frameworkUrl','codeUrl']){
    const match=html.match(new RegExp(field+':\\s*"([^" ]+)"'));
    assert(match,`Missing ${field}`);
    assert(!match[1].startsWith('/')&&!match[1].includes('..'),'Assets must stay relative');
    assert((await stat(path.join(root,match[1]))).size>0,`Missing ${field} asset`);
  }
  console.log(`WEBGL_PACKAGE_OK: ${files.length} files, ${(total/1024/1024).toFixed(1)} MiB`);
}
if(process.argv[1]===new URL(import.meta.url).pathname)await validate(process.argv[2]||'Builds/ORBIT-web');
