export function pageUrl(value) {
  try { const url=new URL(value); if(!['http:','https:'].includes(url.protocol)||url.username||url.password) return null; return url.href; }
  catch { return null; }
}
export function normalizedUrl(value) { const page=pageUrl(value); if(!page) return null; const url=new URL(page); url.hash=''; return url.href; }
export function canRedirect(tab,command) {
  const url=pageUrl(command?.url);
  return !!url && command.action==='block' && tab?.id===command.tabId && tab.active===true &&
    pageUrl(tab.url)===url && (!tab.pendingUrl || pageUrl(tab.pendingUrl)===url);
}
export function validSettings(value) {
  return !!value && Number.isInteger(value.port) && value.port>=1024 && value.port<=65535 && /^[a-f0-9]{64,256}$/i.test(value.token ?? '');
}
export async function request(fetcher,settings,defaults,path,body) {
  if(!validSettings(settings)) throw new Error('尚未配对');
  const response=await fetcher(`http://127.0.0.1:${settings.port}${path}`,{
    method:body===undefined?'GET':'POST', headers:{Authorization:`Bearer ${settings.token}`,...(body===undefined?{}:{'Content-Type':'application/json'})},
    body:body===undefined?undefined:JSON.stringify(body), signal:AbortSignal.timeout(defaults.requestTimeoutMilliseconds),
    cache:'no-store',credentials:'omit',redirect:'error'
  });
  if(!response.ok) throw new Error('连接失败');
  return response.json();
}
