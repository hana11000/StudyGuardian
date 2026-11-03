using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.App;
using StudyGuardian.Core;
using StudyGuardian.Data;

namespace StudyGuardian.Windows.Tests;

[TestClass]
public class SessionControllerTests
{
    [TestMethod]
    public Task BreakStopsCaptureAndRejectsBrowserActivityThenAutomaticallyResumes() => RunDispatcher(async dispatcher=>
    {
        var directory=Path.Combine(Path.GetTempPath(),"StudyGuardian.Windows.Tests",Guid.NewGuid().ToString("N"));
        var root=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"config","defaults.json")))!;
        using(var listener=new TcpListener(IPAddress.Loopback,0)) {listener.Start();root["browser"]!["port"]=((IPEndPoint)listener.LocalEndpoint).Port;}
        root["monitoring"]!["idleSampleMilliseconds"]=10;var json=new JsonSerializerOptions {PropertyNameCaseInsensitive=true};
        T Read<T>(string name)=>root[name]!.Deserialize<T>(json)!;
        var monitoring=Read<MonitoringOptions>("monitoring");var ui=Read<SessionUiOptions>("session");var ai=Read<AiOptions>("ai");
        var intervention=Read<InterventionOptions>("intervention");var browser=Read<BrowserOptions>("browser");var tools=Read<SessionToolsOptions>("sessionTools");
        var clock=new Clock();var foreground=new Foreground();var idle=new Idle();
        try
        {
            var preferences=new UserSettingsStore(directory,intervention);
            preferences.Save(preferences.Current with {GlobalRules=[new("g",RuleKind.Block,RuleMatchType.Process,"game.exe")]});
            await using var controller=new SessionController(monitoring,ui,ai,new(directory,ai),intervention,browser,tools,preferences,Path.Combine(directory,"test.db"),dispatcher,foreground,idle,clock);
            await controller.InitializeAsync();Assert.IsTrue(controller.IsReady);
            await controller.StartAsync(new("合成会话",TimeSpan.FromMinutes(50),SessionStrictness.Standard,[new("study",RuleKind.Allow,RuleMatchType.Process,"notepad.exe")]));
            clock.Advance(5);await Task.Delay(40);Assert.AreEqual(StudyVerdict.OnTask,controller.Snapshot.Decision.Verdict);
            controller.BeginBreak();Assert.IsTrue(controller.IsOnBreak);Assert.IsFalse(foreground.Running);var reads=idle.Reads;var refreshes=foreground.Refreshes;var remaining=controller.Snapshot.Remaining;
            foreground.Publish(new(2,42,"game.exe","合成游戏"));clock.Advance(60);await Task.Delay(40);
            using var http=new HttpClient(new SocketsHttpHandler {UseProxy=false});http.DefaultRequestHeaders.Authorization=new("Bearer",controller.BrowserPairing.ReadOrCreate());
            var status=JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{browser.Port}/status"));Assert.IsFalse(status.RootElement.GetProperty("monitoring").GetBoolean());
            using var response=await http.PostAsync($"http://127.0.0.1:{browser.Port}/activity",new StringContent("{\"title\":\"synthetic\"}",Encoding.UTF8,"application/json"));response.EnsureSuccessStatusCode();
            Assert.AreEqual(reads,idle.Reads);Assert.AreEqual(refreshes,foreground.Refreshes);Assert.AreEqual(remaining,controller.Snapshot.Remaining);Assert.AreEqual(PauseReason.Break,controller.Snapshot.PauseReason);
            clock.Advance(tools.BreakMinutes*60-60);await Task.Delay(40);Assert.IsFalse(controller.IsOnBreak);Assert.IsTrue(foreground.Running);
            clock.Advance(5);await Task.Delay(40);Assert.AreEqual(StudyVerdict.Distracted,controller.Snapshot.Decision.Verdict);
            Assert.IsTrue(controller.UseEmergencyPass());Assert.AreEqual(2,controller.Tools.EmergencyUsesRemaining);Assert.AreEqual(DecisionSource.Override,controller.Snapshot.Decision.Source);
            clock.Advance(2);await Task.Delay(30);await controller.StopAsync();
            var report=await controller.ReadReportAsync((await controller.ReadReportsAsync()).Single());Assert.IsTrue(report.Paused>=TimeSpan.FromMinutes(tools.BreakMinutes));Assert.IsTrue(report.Supportive>=TimeSpan.FromSeconds(2));
        }
        finally {if(Directory.Exists(directory)) Directory.Delete(directory,true);}
    });
    internal static Task RunDispatcher(Func<Dispatcher,Task> body)
    {
        var completed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            var dispatcher=Dispatcher.CurrentDispatcher;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(new Action(async()=>
            {try {await body(dispatcher);completed.TrySetResult();}catch(Exception e){completed.TrySetException(e);}finally{dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);}}));
            Dispatcher.Run();
        }) {IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return completed.Task;
    }
    private sealed class Clock : IClock
    {public DateTimeOffset UtcNow {get;private set;}=new(2026,11,3,0,0,0,TimeSpan.Zero);public TimeSpan Elapsed {get;private set;}public void Advance(double seconds){Elapsed+=TimeSpan.FromSeconds(seconds);UtcNow+=TimeSpan.FromSeconds(seconds);}}
    private sealed class Idle : IIdleTimeProvider
    {public int Reads;public TimeSpan GetIdleTime(){Reads++;return TimeSpan.Zero;}}
    private sealed class Foreground : IForegroundWindowMonitor
    {
        public WindowSnapshot Current {get;private set;}=new(1,42,"notepad.exe","合成学习");public bool Running;public int Refreshes;
        public event EventHandler<WindowSnapshot>? Changed;
        public void Start()=>Running=true;public void Stop()=>Running=false;public WindowSnapshot Refresh(){Refreshes++;return Current;}
        public void Publish(WindowSnapshot window){Current=window;Changed?.Invoke(this,window);}public void Dispose()=>Stop();
    }
}
