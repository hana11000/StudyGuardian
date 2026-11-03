import {defaults} from './defaults.mjs';
import {request} from './protocol.mjs';
const tab=await chrome.tabs.getCurrent();
const saved=await chrome.storage.session.get('blocked:'+tab.id);
document.querySelector('#goal').textContent=saved['blocked:'+tab.id]?.goal ?? '';
document.querySelector('#return').addEventListener('click',async()=>{
  const status=document.querySelector('#status');
  try { await request(fetch,await chrome.storage.local.get(['port','token']),defaults,'/return',{}); status.textContent=''; }
  catch { status.textContent='请打开 Study Guardian'; }
});
