using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.App;
using StudyGuardian.Core;
using StudyGuardian.Data;

namespace StudyGuardian.Windows.Tests;

[TestClass]
public sealed class RuntimeAiTests
{
    [TestMethod]
    public Task GoalAloneCallsAiAfterDwellAndRetriesFailureOnSameWindow() => WithController(async (controller, clock, foreground, ai) =>
    {
        ai.Fail = true;
        await controller.StartAsync(new("学习税法", TimeSpan.FromMinutes(50), SessionStrictness.Standard, []));
        clock.Advance(4); await Task.Delay(80); Assert.AreEqual(0, ai.Calls);
        StringAssert.Contains(controller.AiStatus, "1 秒");
        clock.Advance(1); await Task.Delay(80);
        Assert.AreEqual(1, ai.Calls);
        Assert.AreEqual(DecisionSource.Fallback, controller.Snapshot.Decision.Source);
        StringAssert.Contains(controller.AiStatus, "AI 请求超时");
        Assert.IsFalse(controller.Snapshot.Decision.CanEnforce);
        clock.Advance(29); await Task.Delay(80); Assert.AreEqual(1, ai.Calls);
        ai.Fail = false; clock.Advance(1); await Task.Delay(100);
        Assert.AreEqual(2, ai.Calls);
        Assert.AreEqual(StudyVerdict.OnTask, controller.Snapshot.Decision.Verdict);
        StringAssert.Contains(controller.AiStatus, "2 次");
        foreground.Publish(new(2,43,"editor.exe","第二个学习窗口"));
        clock.Advance(5); await Task.Delay(80); Assert.AreEqual(3, ai.Calls);
        foreground.Publish(Foreground.Study); clock.Advance(5); await Task.Delay(80);
        Assert.AreEqual(3, ai.Calls); Assert.AreEqual(DecisionSource.Cache, controller.Snapshot.Decision.Source);
        await controller.StopAsync();
        var report = await controller.ReadReportAsync((await controller.ReadReportsAsync()).Single());
        Assert.IsTrue(report.Supportive >= TimeSpan.FromSeconds(30));
    });

    [TestMethod]
    public Task UnexpectedFailureAlsoBacksOffAndRetries() => WithController(async (controller, clock, foreground, ai) =>
    {
        ai.UnexpectedFailure = true;
        await controller.StartAsync(new("学习",TimeSpan.FromMinutes(50),SessionStrictness.Standard,[]));
        clock.Advance(5); await Task.Delay(80);
        Assert.AreEqual(DecisionSource.Fallback,controller.Snapshot.Decision.Source);
        StringAssert.Contains(controller.AiStatus,"30 秒后重试");
        ai.UnexpectedFailure=false; clock.Advance(30); await Task.Delay(80);
        Assert.AreEqual(2,ai.Calls);
        Assert.AreEqual(StudyVerdict.OnTask,controller.Snapshot.Decision.Verdict);
    });

    [TestMethod]
    public Task LeavingWindowCancelsRequestAndLateReplyCannotApply() => WithController(async (controller, clock, foreground, ai) =>
    {
        ai.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await controller.StartAsync(new("学习", TimeSpan.FromMinutes(50), SessionStrictness.Standard, []));
        clock.Advance(5); await Task.Delay(80); Assert.AreEqual(1, ai.Calls);
        StringAssert.Contains(controller.AiStatus, "判断中");
        foreground.Publish(new(2,43,"editor.exe","新窗口"));
        Assert.IsTrue(ai.Token.IsCancellationRequested);
        ai.Pending.SetResult(new(StudyVerdict.Distracted,.99,"合成结果",CacheScope.Title,0));
        await Task.Delay(80);
        Assert.AreEqual(StudyVerdict.Unknown,controller.Snapshot.Decision.Verdict);
        await controller.SetPausedAsync(true); clock.Advance(30); await Task.Delay(80);
        Assert.AreEqual(1,ai.Calls); StringAssert.Contains(controller.AiStatus,"暂停");
    });

    private static Task WithController(Func<SessionController, Clock, Foreground, Ai, Task> body) => SessionControllerTests.RunDispatcher(async dispatcher =>
    {
        var directory = Path.Combine(Path.GetTempPath(),"StudyGuardian.Windows.Tests",Guid.NewGuid().ToString("N"));
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"config","defaults.json")))!;
        using(var listener=new TcpListener(IPAddress.Loopback,0)) { listener.Start(); root["browser"]!["port"]=((IPEndPoint)listener.LocalEndpoint).Port; }
        root["monitoring"]!["idleSampleMilliseconds"]=10;
        var json=new JsonSerializerOptions {PropertyNameCaseInsensitive=true};
        T Read<T>(string name)=>root[name]!.Deserialize<T>(json)!;
        var options=Read<AiOptions>("ai"); var intervention=Read<InterventionOptions>("intervention");
        var clock=new Clock(); var foreground=new Foreground(); var ai=new Ai();
        try
        {
            var settings=new AiSettingsStore(directory,options); settings.Save(new(true,"synthetic-model"),"synthetic-key",false);
            await using var controller=new SessionController(Read<MonitoringOptions>("monitoring"),Read<SessionUiOptions>("session"),options,settings,intervention,
                Read<BrowserOptions>("browser"),Read<SessionToolsOptions>("sessionTools"),new UserSettingsStore(directory,intervention),Path.Combine(directory,"test.db"),
                dispatcher,foreground,new Idle(),clock,ai);
            await controller.InitializeAsync(); Assert.IsTrue(controller.IsReady);
            await body(controller,clock,foreground,ai);
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory,true); }
    });
    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow {get;private set;}=new(2026,11,3,0,0,0,TimeSpan.Zero);
        public TimeSpan Elapsed {get;private set;}
        public void Advance(double seconds) {Elapsed+=TimeSpan.FromSeconds(seconds);UtcNow+=TimeSpan.FromSeconds(seconds);}
    }
    private sealed class Idle : IIdleTimeProvider { public TimeSpan GetIdleTime()=>TimeSpan.Zero; }
    private sealed class Foreground : IForegroundWindowMonitor
    {
        public static readonly WindowSnapshot Study=new(1,42,"notepad.exe","税法学习");
        public WindowSnapshot Current {get;private set;}=Study;
        public event EventHandler<WindowSnapshot>? Changed;
        public void Start(){} public void Stop(){} public void Dispose(){}
        public WindowSnapshot Refresh()=>Current;
        public void Publish(WindowSnapshot window) {Current=window;Changed?.Invoke(this,window);}
    }
    private sealed class Ai : IStudyAi
    {
        public bool Fail; public bool UnexpectedFailure; public int Calls; public CancellationToken Token;
        public TaskCompletionSource<AiDecision>? Pending;
        public Task<StudyPolicy> GeneratePolicyAsync(string goal,CancellationToken cancellationToken)=>throw new NotSupportedException();
        public async Task<AiDecision> ClassifyAsync(DecisionRequest request,CancellationToken cancellationToken)
        {
            Calls++; Token=cancellationToken; await Task.Yield();
            if(UnexpectedFailure) throw new InvalidOperationException("synthetic failure");
            if(Fail) throw new AiUnavailableException("AI 请求超时");
            if(Pending is not null) return await Pending.Task;
            return new(StudyVerdict.OnTask,.99,"学习资料",CacheScope.Title,30);
        }
    }
}
