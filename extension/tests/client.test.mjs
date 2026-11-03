import test from 'node:test';
import assert from 'node:assert/strict';
import { BrowserClient } from '../browser-client.mjs';
import { canRedirect, normalizedUrl, validSettings } from '../protocol.mjs';
import {readFileSync} from 'node:fs';
import {defaults as generatedDefaults} from '../defaults.mjs';

const settings={port:18731,token:'a'.repeat(64)};
const defaults={requestTimeoutMilliseconds:3000,maximumUrlCharacters:4096,maximumTitleCharacters:2048};
function fixture(monitoring=true, command={action:'none'}) {
  const calls=[],updates=[],session={}; let reads=0;
  const tab={id:5,windowId:1,url:'https://www.bilibili.com/video/game',title:'游戏实况',active:true};
  const chrome={
    storage:{local:{get:async()=>settings},session:{get:async()=>session,set:async v=>Object.assign(session,v)}},
    windows:{getLastFocused:async()=>{reads++;return {id:1,focused:true,type:'normal'};}},
    tabs:{query:async()=>[tab],get:async()=>tab,update:async(id,value)=>updates.push({id,...value})},
    runtime:{getURL:path=>'chrome-extension://'+'a'.repeat(32)+'/'+path},action:{setBadgeText:async()=>{}}
  };
  const fetcher=async(url,init)=>{ calls.push({url,init}); return {ok:true,json:async()=>url.endsWith('/status')?{monitoring}:url.endsWith('/command')?command:{action:'none'}}; };
  return {client:new BrowserClient(chrome,fetcher,defaults,'chrome.exe'),calls,updates,tab,reads:()=>reads};
}
test('stale block cannot redirect a new or pending page',()=>{
  const command={action:'block',tabId:5,url:'https://example.com/game'};
  assert.equal(canRedirect({id:5,active:true,url:command.url},command),true);
  for(const tab of [{id:6,active:true,url:command.url},{id:5,active:false,url:command.url},{id:5,active:true,url:'https://example.com/study'},{id:5,active:true,url:command.url,pendingUrl:'https://example.com/study'}]) assert.equal(canRedirect(tab,command),false);
});
test('only HTTP URLs without credentials and valid local pairing settings',()=>{
  assert.equal(normalizedUrl('https://EXAMPLE.com/game#part'),'https://example.com/game');
  for(const url of ['chrome://settings','file:///test','javascript:alert(1)','https://user:pass@example.com/']) assert.equal(normalizedUrl(url),null);
  assert.equal(validSettings(settings),true); assert.equal(validSettings({...settings,port:80}),false); assert.equal(validSettings({...settings,token:'short'}),false);
});
test('stopped session does not even read tabs',async()=>{ const f=fixture(false); await f.client.tick(); assert.equal(f.reads(),0); assert.equal(f.calls.length,1); assert.ok(f.calls[0].url.endsWith('/status')); });
test('active session reports focused URL and uses pairing header',async()=>{ const f=fixture(); await f.client.tick(); const call=f.calls.find(c=>c.url.endsWith('/activity')); assert.ok(call); const body=JSON.parse(call.init.body); assert.equal(body.url,f.tab.url); assert.equal(body.focused,true); assert.equal(body.process,'chrome.exe'); assert.equal(call.init.headers.Authorization,'Bearer '+settings.token); });
test('pending block is delivered even after desktop minimized browser',async()=>{ const f=fixture(true,{action:'block',tabId:5,url:'https://www.bilibili.com/video/game',goal:'税法'}); await f.client.tick(); assert.equal(f.updates.length,1); assert.ok(f.updates[0].url.endsWith('/blocked.html')); assert.ok(!f.updates[0].url.includes('bilibili')); });
test('old command does not redirect changed tab',async()=>{ const f=fixture(true,{action:'block',tabId:5,url:'https://www.bilibili.com/video/previous'}); await f.client.tick(); assert.equal(f.updates.length,0); });
test('offline desktop never reads tabs or redirects',async()=>{ const f=fixture(); f.client.fetcher=async()=>{throw new Error('offline');}; await assert.rejects(f.client.tick()); assert.equal(f.reads(),0); assert.equal(f.updates.length,0); });
test('configuration is generated from root defaults',()=>{ const root=JSON.parse(readFileSync(new URL('../../config/defaults.json',import.meta.url),'utf8')); assert.deepEqual(generatedDefaults,root.browser); });
test('navigation during command handling is checked again',async()=>{ const f=fixture(); const set=f.client.chrome.storage.session.set; f.client.chrome.storage.session.set=async value=>{ await set(value); if(value['blocked:5']) f.tab.url='https://example.com/study'; }; await f.client.applyCommand({action:'block',tabId:5,url:f.tab.url}); assert.equal(f.updates.length,0); });
test('block authority distinguishes current and pending fragment routes',()=>{
  const command={action:'block',tabId:5,url:'https://example.com/app#/game'};
  assert.equal(canRedirect({id:5,active:true,url:command.url},command),true);
  assert.equal(canRedirect({id:5,active:true,url:'https://example.com/app#/study'},command),false);
  assert.equal(canRedirect({id:5,active:true,url:command.url,pendingUrl:'https://example.com/app#/study'},command),false);
});
test('activity retains the full URL used to target a command',async()=>{const f=fixture();f.tab.url='https://example.com/app#/game';await f.client.tick();const call=f.calls.find(c=>c.url.endsWith('/activity'));assert.equal(JSON.parse(call.init.body).url,f.tab.url);});
