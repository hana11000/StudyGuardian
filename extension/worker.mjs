import {BrowserClient} from './browser-client.mjs';
import {defaults} from './defaults.mjs';

const client=new BrowserClient(chrome,fetch,defaults,navigator.userAgent.includes('Edg/')?'msedge.exe':'chrome.exe');
let running=false,again=false,timer;
async function poll() {
  if(running) { again=true; return; }
  running=true; clearTimeout(timer);
  let active=false;
  try { active=await client.tick(); }
  catch { await chrome.action.setBadgeText({text:''}).catch(()=>{}); }
  finally {
    running=false;
    const delay=again?0:active?defaults.pollMilliseconds:defaults.heartbeatSeconds*1000;
    again=false; timer=setTimeout(poll,delay);
  }
}
async function ensureAlarm() {
  await chrome.storage.local.setAccessLevel({accessLevel:'TRUSTED_CONTEXTS'});
  if(!await chrome.alarms.get('heartbeat')) await chrome.alarms.create('heartbeat',{periodInMinutes:defaults.heartbeatSeconds/60});
}
chrome.tabs.onActivated.addListener(()=>void poll());
chrome.tabs.onUpdated.addListener((_id,change,tab)=>{ if(tab.active && (change.url || change.title || change.status)) void poll(); });
chrome.windows.onFocusChanged.addListener(()=>void poll());
chrome.alarms.onAlarm.addListener(alarm=>{ if(alarm.name==='heartbeat') void poll(); });
chrome.storage.onChanged.addListener((_changes,area)=>{ if(area==='local') void poll(); });
chrome.action.onClicked.addListener(()=>void chrome.runtime.openOptionsPage());
chrome.runtime.onInstalled.addListener(()=>{ void ensureAlarm(); void poll(); });
chrome.runtime.onStartup.addListener(()=>{ void ensureAlarm(); void poll(); });
void ensureAlarm().then(poll).catch(()=>{});
