using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;
using StudyGuardian.Monitor;

namespace StudyGuardian.Windows.Tests;

[TestClass]
public class LoopbackBrowserServerTests
{
    private static BrowserOptions Options(int port) => new() { Port=port, TokenBytes=32, MaximumHeaderBytes=4096, MaximumBodyBytes=1024,
        MaximumConnections=2, RequestTimeoutMilliseconds=1000, PollMilliseconds=2000, HeartbeatSeconds=30, FreshnessSeconds=5,
        CommandTtlSeconds=30, MaximumClients=8, MaximumUrlCharacters=4096, MaximumTitleCharacters=2048,
        Processes=["chrome.exe"],TitleSuffixes=[" - Google Chrome"] };
    private static int FreePort() { using var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); return ((IPEndPoint)listener.LocalEndpoint).Port; }
    private const string Token="synthetic-browser-pairing-token-0123456789";
    private static HttpClient Client(int port,string? token=Token)
    { var client=new HttpClient(new SocketsHttpHandler { UseProxy=false }) {BaseAddress=new($"http://127.0.0.1:{port}"),Timeout=TimeSpan.FromSeconds(5)}; if(token!=null) client.DefaultRequestHeaders.Authorization=new("Bearer",token); return client; }

    [TestMethod] public async Task AuthenticatedStatusAndActivityAreHandledWhileMissingAndWrongTokenAreRejected()
    {
        var port=FreePort(); var calls=0;
        await using var server=new LoopbackBrowserServer(Options(port),()=>Token,(r,_)=>{calls++;return Task.FromResult<object>(new{action="none",path=r.Path});}); server.Start();
        using var good=Client(port); Assert.AreEqual(HttpStatusCode.OK,(await good.GetAsync("/status")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK,(await good.PostAsync("/activity",new StringContent("{\"tabId\":1}",Encoding.UTF8,"application/json"))).StatusCode);
        using var absent=Client(port,null); using var wrong=Client(port,"wrong");
        Assert.AreEqual(HttpStatusCode.Unauthorized,(await absent.GetAsync("/status")).StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized,(await wrong.GetAsync("/status")).StatusCode); Assert.AreEqual(2,calls);
    }
    [TestMethod] public async Task WebOriginWrongHostLargePayloadAndUnknownPathsNeverReachHandler()
    {
        var port=FreePort(); var calls=0;
        await using var server=new LoopbackBrowserServer(Options(port),()=>Token,(_,_)=>{calls++;return Task.FromResult<object>(new{});}); server.Start(); using var client=Client(port);
        using var web=new HttpRequestMessage(HttpMethod.Get,"/status"); web.Headers.Add("Origin","https://evil.example");
        Assert.AreEqual(HttpStatusCode.Forbidden,(await client.SendAsync(web)).StatusCode);
        using var host=new HttpRequestMessage(HttpMethod.Get,"/status"); host.Headers.Host="evil.example";
        Assert.AreEqual(HttpStatusCode.Forbidden,(await client.SendAsync(host)).StatusCode);
        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge,(await client.PostAsync("/activity",new StringContent(new string('x',2048),Encoding.UTF8,"application/json"))).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound,(await client.GetAsync("/unknown")).StatusCode); Assert.AreEqual(0,calls);
    }
    [TestMethod] public async Task TokenRotationTakesEffectAndDisposeReleasesPort()
    {
        var port=FreePort(); var token=Token;
        await using(var server=new LoopbackBrowserServer(Options(port),()=>token,(_,_)=>Task.FromResult<object>(new{})))
        { server.Start(); using var client=Client(port); Assert.AreEqual(HttpStatusCode.OK,(await client.GetAsync("/status")).StatusCode); token="rotated-synthetic-token"; Assert.AreEqual(HttpStatusCode.Unauthorized,(await client.GetAsync("/status")).StatusCode); }
        using var listener=new TcpListener(IPAddress.Loopback,port); listener.Start();
    }
    [TestMethod] public async Task MalformedJsonAndChunkedRequestsAreRejected()
    {
        var port=FreePort(); var calls=0;
        await using var server=new LoopbackBrowserServer(Options(port),()=>Token,(_,_)=>{calls++;return Task.FromResult<object>(new{});}); server.Start(); using var client=Client(port);
        Assert.AreEqual(HttpStatusCode.BadRequest,(await client.PostAsync("/activity",new StringContent("{",Encoding.UTF8,"application/json"))).StatusCode);
        using var chunked=new HttpRequestMessage(HttpMethod.Post,"/activity") { Content=new StringContent("{}",Encoding.UTF8,"application/json") }; chunked.Headers.TransferEncodingChunked=true;
        Assert.AreEqual(HttpStatusCode.BadRequest,(await client.SendAsync(chunked)).StatusCode); Assert.AreEqual(0,calls);
    }
}
