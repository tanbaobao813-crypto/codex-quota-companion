import readline from 'node:readline';
import {resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {start,stop,status} from './control.mjs';

export const toolDefinitions=[
  {name:'quota_companion_start',description:'Show the Windows quota companion beside the running Codex client.',inputSchema:{type:'object',properties:{},additionalProperties:false},annotations:{readOnlyHint:false,destructiveHint:false}},
  {name:'quota_companion_stop',description:'Close only the quota companion. Keep the Codex client open.',inputSchema:{type:'object',properties:{},additionalProperties:false},annotations:{readOnlyHint:false,destructiveHint:false}},
  {name:'quota_companion_status',description:'Read the locally displayed quota snapshot and companion visibility. No new quota request is made.',inputSchema:{type:'object',properties:{},additionalProperties:false},annotations:{readOnlyHint:true,destructiveHint:false}}
];
export async function handle(message,controls={start,stop,status}){
  if(!message||message.jsonrpc!=='2.0')return {jsonrpc:'2.0',id:message?.id??null,error:{code:-32600,message:'Invalid JSON-RPC request'}};
  if(message.id===undefined)return null;
  let result;
  if(message.method==='initialize')result={protocolVersion:['2024-11-05','2025-03-26','2025-06-18'].includes(message.params?.protocolVersion)?message.params.protocolVersion:'2024-11-05',capabilities:{tools:{}},serverInfo:{name:'codex-quota-companion',version:'0.1.1'}};
  else if(message.method==='ping')result={};
  else if(message.method==='tools/list')result={tools:toolDefinitions};
  else if(message.method==='tools/call'){
    const args=message.params?.arguments||{};
    if(typeof args!=='object'||Array.isArray(args)||Object.keys(args).length)return {jsonrpc:'2.0',id:message.id,error:{code:-32602,message:'This tool accepts no arguments'}};
    const action=toolDefinitions.some(t=>t.name===message.params?.name)?{quota_companion_start:controls.start,quota_companion_stop:controls.stop,quota_companion_status:controls.status}[message.params.name]:null;
    if(!action)return {jsonrpc:'2.0',id:message.id,error:{code:-32602,message:'Unknown tool'}};
    try{result={content:[{type:'text',text:JSON.stringify(await action())}]};}catch{result={isError:true,content:[{type:'text',text:'The Windows companion is unavailable. Check the release executable and Codex client.'}]};}
  }else return {jsonrpc:'2.0',id:message.id,error:{code:-32601,message:'Method not found'}};
  return {jsonrpc:'2.0',id:message.id,result};
}
const direct=process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url);
if(direct){
  const lines=readline.createInterface({input:process.stdin,crlfDelay:Infinity});
  for await(const line of lines){
    if(!line.trim())continue;
    try{const response=await handle(JSON.parse(line));if(response)process.stdout.write(JSON.stringify(response)+'\n');}
    catch{process.stdout.write(JSON.stringify({jsonrpc:'2.0',id:null,error:{code:-32700,message:'Invalid JSON'}})+'\n');}
  }
}
