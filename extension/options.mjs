import {defaults} from './defaults.mjs';
import {validSettings,request} from './protocol.mjs';
const form=document.querySelector('#pairing'),port=document.querySelector('#port'),token=document.querySelector('#token'),status=document.querySelector('#status'),button=document.querySelector('#connect');
const saved=await chrome.storage.local.get(['port','token']); port.value=saved.port ?? defaults.port;
if(validSettings(saved)) { status.textContent='已配对'; status.className='status success'; token.required=false; }
form.addEventListener('submit',async event=>{
  event.preventDefault(); const settings={port:Number(port.value),token:token.value.trim()||saved.token};
  if(!validSettings(settings)) { status.textContent='请检查端口和配对码'; status.className='status error'; return; }
  button.disabled=true;
  try { await request(fetch,settings,defaults,'/status'); await chrome.storage.local.set(settings); Object.assign(saved,settings); token.value=''; token.required=false; status.textContent='已连接'; status.className='status success'; }
  catch { status.textContent='连接失败，请检查桌面程序和配对码'; status.className='status error'; }
  finally { button.disabled=false; }
});
document.querySelector('#disconnect').addEventListener('click',async()=>{
  await chrome.storage.local.remove(['port','token']); await chrome.storage.session.clear(); saved.token=null; token.value=''; token.required=true; status.textContent='已断开'; status.className='status';
});
