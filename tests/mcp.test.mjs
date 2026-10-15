import test from 'node:test';
import assert from 'node:assert/strict';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {handle} from '../plugins/codex-quota-companion/scripts/mcp.mjs';
import {safeStatus,privateDataDirectory} from '../plugins/codex-quota-companion/scripts/control.mjs';

const request=(name,args={})=>({jsonrpc:'2.0',id:1,method:'tools/call',params:{name,arguments:args}});
test('MCP initializes and publishes only fixed companion tools',async()=>{
  const init=await handle({jsonrpc:'2.0',id:1,method:'initialize',params:{protocolVersion:'2025-03-26'}});
  assert.equal(init.result.protocolVersion,'2025-03-26');
  assert.equal(init.result.serverInfo.version,'0.1.1');
  const tools=await handle({jsonrpc:'2.0',id:2,method:'tools/list'});assert.equal(tools.result.tools.length,3);
  assert.equal(await handle({jsonrpc:'2.0',method:'notifications/initialized'}),null);
});
test('start stop and status dispatch with sanitized responses',async()=>{
  let starts=0,stops=0;const controls={start:async()=>({started:++starts>0}),stop:async()=>({stopped:++stops>0}),status:async()=>({attached:true})};
  assert.equal((await handle(request('quota_companion_start'),controls)).result.isError,undefined);
  await handle(request('quota_companion_stop'),controls);assert.equal(starts,1);assert.equal(stops,1);
  const result=await handle(request('quota_companion_status'),controls);assert.equal(JSON.parse(result.result.content[0].text).attached,true);
});
test('unknown tools, prototype names and arbitrary arguments cannot execute',async()=>{
  let calls=0;const controls={start:async()=>calls++,stop:async()=>calls++,status:async()=>calls++};
  for(const name of ['constructor','toString','__proto__','shell'])assert.equal((await handle(request(name),controls)).error.code,-32602);
  assert.equal((await handle(request('quota_companion_start',{command:'anything'}),controls)).error.code,-32602);assert.equal(calls,0);
});
test('tool failures never echo private paths or credentials',async()=>{
  const r=await handle(request('quota_companion_status'),{status:async()=>{throw Error('private-token-marker');}});
  assert.equal(r.result.isError,true);assert.ok(!JSON.stringify(r).includes('private-token-marker'));
});
test('status excludes native paths, handles and unrecognized rows',()=>{
  const r=safeStatus([{kind:'whale',visible:true,livePrimary:50,liveSecondary:70,path:'private-path',owner:99},{kind:'text',path:'secret'}]);
  assert.equal(r.windows.length,1);assert.equal(r.windows[0].remainingPrimary,50);assert.ok(!JSON.stringify(r).includes('private-path'));assert.ok(!JSON.stringify(r).includes('owner'));
  assert.equal(r.version,'0.1.1');
});
test('plugin data uses configured writable storage',()=>{
  assert.equal(privateDataDirectory({PLUGIN_DATA:'fixture-data'}),'fixture-data');assert.equal(privateDataDirectory({CODEX_QUOTA_DATA_DIR:'override',PLUGIN_DATA:'other'}),'override');
});
test('actual stdio transport survives invalid JSON and initializes',async()=>{
  const entry=fileURLToPath(new URL('../plugins/codex-quota-companion/scripts/mcp.mjs',import.meta.url));
  const child=spawn(process.execPath,[entry],{stdio:['pipe','pipe','pipe']});let output='',errors='';child.stdout.on('data',b=>output+=b);child.stderr.on('data',b=>errors+=b);
  child.stdin.end('invalid-json\n'+JSON.stringify({jsonrpc:'2.0',id:9,method:'initialize',params:{protocolVersion:'2024-11-05'}})+'\n');
  const code=await new Promise(r=>child.once('close',r));assert.equal(code,0,errors);const rows=output.trim().split('\n').map(JSON.parse);assert.equal(rows[0].error.code,-32700);assert.equal(rows[1].result.serverInfo.name,'codex-quota-companion');
});
