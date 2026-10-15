import {execFile, spawn} from 'node:child_process';
import {existsSync} from 'node:fs';
import {dirname,resolve,join} from 'node:path';
import {fileURLToPath} from 'node:url';

export const pluginRoot=resolve(dirname(fileURLToPath(import.meta.url)),'..');
export const executable=join(pluginRoot,'bin','CodexQuotaWidget.exe');
export function privateDataDirectory(env=process.env){
  return env.CODEX_QUOTA_DATA_DIR || env.PLUGIN_DATA || env.CLAUDE_PLUGIN_DATA || join(env.LOCALAPPDATA || env.USERPROFILE || pluginRoot,'CodexQuotaCompanion');
}
function invoke(args){
  if(process.platform!=='win32')return Promise.reject(new Error('This companion requires Windows.'));
  if(!existsSync(executable))return Promise.reject(new Error('The packaged executable is missing. Build the project or use the release ZIP.'));
  return new Promise((ok,bad)=>execFile(executable,args,{windowsHide:true,timeout:15000,maxBuffer:1024*1024,env:{...process.env,CODEX_QUOTA_DATA_DIR:privateDataDirectory()}},(error,output)=>error?bad(new Error('Companion command failed.')):ok(output)));
}
export function safeStatus(rows){
  return {version:'0.1.1',attached:rows.some(x=>x.kind==='whale'),visible:rows.some(x=>x.kind==='whale'&&x.visible),windows:rows.filter(x=>x.kind==='whale'||x.kind==='bubble').map(x=>({kind:x.kind,visible:Boolean(x.visible),remainingPrimary:x.livePrimary??null,remainingSecondary:x.liveSecondary??null,updatedAt:x.updatedAt||null,primaryResetAt:x.primaryResetAt||null,secondaryResetAt:x.secondaryResetAt||null,syncError:Boolean(x.syncError)}))};
}
export async function status(){return safeStatus(JSON.parse(await invoke(['--dock-snapshot'])));}
export async function start({launchClient=false}={}){
  const current=await status();if(current.attached)return {...current,alreadyRunning:true};
  await new Promise((ok,bad)=>{
    // This is a GUI executable: hiding its startup window also hides the avatar.
    const child=spawn(executable,launchClient?['--launch-client']:[],{windowsHide:false,detached:true,stdio:'ignore',env:{...process.env,CODEX_QUOTA_DATA_DIR:privateDataDirectory()}});
    child.once('error',()=>bad(new Error('Unable to start the companion.')));child.once('spawn',()=>{child.unref();ok();});
  });
  return {started:true,waitingForClient:true};
}
export async function stop(){await invoke(['--stop']);return {stopped:true,clientClosed:false};}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  const action=process.argv[2]||'status';
  try{
    const result=action==='open'?await start():action==='launch'?await start({launchClient:true}):action==='stop'?await stop():action==='status'?await status():null;
    if(!result)throw new Error('Use open, launch, stop or status.');
    process.stdout.write(JSON.stringify(result)+'\n');
  }catch(error){process.stderr.write(error.message+'\n');process.exitCode=1;}
}
