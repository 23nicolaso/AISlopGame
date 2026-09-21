import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,writeFile,mkdir,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {validate} from './check-webgl.mjs';
async function fixture(fn){const root=await mkdtemp(path.join(tmpdir(),'orbit-webgl-'));try{
  await mkdir(path.join(root,'Build'));
  for(const name of ['data','framework','wasm'])await writeFile(path.join(root,'Build',name),'test');
  await writeFile(path.join(root,'index.html'),'matchWebGLToCanvasSize:true dataUrl:"Build/data" frameworkUrl:"Build/framework" codeUrl:"Build/wasm"');
  await fn(root);
}finally{await rm(root,{recursive:true,force:true});}}
test('accepts complete relative bundle',()=>fixture(validate));
test('rejects missing wasm',()=>fixture(async root=>{await rm(path.join(root,'Build/wasm'));await assert.rejects(validate(root));}));
test('rejects unexpanded template',()=>fixture(async root=>{await writeFile(path.join(root,'index.html'),'{{{ PRODUCT_NAME }}}');await assert.rejects(validate(root));}));
test('rejects nonresponsive canvas',()=>fixture(async root=>{await writeFile(path.join(root,'index.html'),'canvas');await assert.rejects(validate(root));}));
test('rejects absolute asset urls',()=>fixture(async root=>{await writeFile(path.join(root,'index.html'),'matchWebGLToCanvasSize:true dataUrl:"/Build/data" frameworkUrl:"Build/framework" codeUrl:"Build/wasm"');await assert.rejects(validate(root));}));
