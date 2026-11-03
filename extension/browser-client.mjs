import {pageUrl,canRedirect,validSettings,request} from './protocol.mjs';
export class BrowserClient {
  constructor(chrome,fetcher,defaults,process) { this.chrome=chrome; this.fetcher=fetcher; this.defaults=defaults; this.process=process; }
  async tick() {
    const settings=await this.chrome.storage.local.get(['port','token']);
    if(!validSettings(settings)) return false;
    const call=(path,body)=>request(this.fetcher,settings,this.defaults,path,body);
    const status=await call('/status');
    if(status.monitoring!==true) { await this.chrome.action.setBadgeText({text:''}); return false; }
    const state=await this.chrome.storage.session.get(['clientId','sequence']);
    const clientId=state.clientId ?? crypto.randomUUID().replaceAll('-','');
    const sequence=(state.sequence ?? 0)+1;
    await this.chrome.storage.session.set({clientId,sequence});
    await this.applyCommand(await call('/command',{clientId}));
    const window=await this.chrome.windows.getLastFocused();
    if(!window.focused || window.type!=='normal') return true;
    const [tab]=await this.chrome.tabs.query({active:true,windowId:window.id});
    const url=pageUrl(tab?.url);
    if(!url || !tab.title || tab.incognito || url.length>this.defaults.maximumUrlCharacters || tab.title.length>this.defaults.maximumTitleCharacters ||
        (tab.pendingUrl && pageUrl(tab.pendingUrl)!==url)) return true;
    const result=await call('/activity',{clientId,sequence,process:this.process,windowId:window.id,tabId:tab.id,title:tab.title,url,focused:true});
    await this.chrome.action.setBadgeText({tabId:tab.id,text:result.action==='warn'?'!':''});
    await this.applyCommand(result);
    return true;
  }
  async applyCommand(command) {
    if(command?.action!=='block' || !Number.isInteger(command.tabId)) return;
    let tab; try { tab=await this.chrome.tabs.get(command.tabId); } catch { return; }
    if(!canRedirect(tab,command)) return;
    await this.chrome.storage.session.set({['blocked:'+tab.id]:{goal:typeof command.goal==='string'?command.goal:''}});
    // Re-read after the storage await: a navigation may have started meanwhile.
    try { tab=await this.chrome.tabs.get(command.tabId); } catch { return; }
    if(canRedirect(tab,command)) await this.chrome.tabs.update(tab.id,{url:this.chrome.runtime.getURL('blocked.html')});
  }
}
