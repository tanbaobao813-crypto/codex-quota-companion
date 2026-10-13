import {start} from './control.mjs';
// Hook output never includes conversations, file paths or credentials.
if(process.platform==='win32'){
  try{await start();}catch{process.stderr.write('Quota companion did not start; use its release executable or build it first.\n');}
}
