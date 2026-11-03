using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;

namespace StudyGuardian.Core.Tests;

[TestClass]
public class BrowserActivityTrackerTests
{
    private readonly FakeClock _clock = new();
    private static BrowserOptions Options => new() { Port=18731, TokenBytes=32, MaximumHeaderBytes=8192, MaximumBodyBytes=16384,
        MaximumConnections=8, RequestTimeoutMilliseconds=3000, PollMilliseconds=2000, HeartbeatSeconds=30, FreshnessSeconds=5,
        CommandTtlSeconds=30, MaximumClients=8, MaximumUrlCharacters=4096, MaximumTitleCharacters=2048,
        Processes=["chrome.exe","msedge.exe"], TitleSuffixes=[" - Google Chrome"," - Microsoft Edge"] };
    private static WindowSnapshot Native => new(42, 7, "chrome.exe", "增值税 - Google Chrome");
    private static BrowserActivity Report => new("0123456789abcdef0123456789abcdef", 1, "chrome.exe", 1, 5, "增值税", "https://www.bilibili.com/video/study", true);

    [TestMethod] public void FocusedMatchingReportSuppliesExactUrl()
    { var tracker=new BrowserActivityTracker(_clock,Options); Assert.IsTrue(tracker.Accept(Report,Native)); Assert.AreEqual(Report.Url,tracker.Enrich(Native).Url); }
    [TestMethod] public void PrefixCollisionCannotBindOldPageToNewTitle()
    { var tracker=new BrowserActivityTracker(_clock,Options); Assert.IsFalse(tracker.Accept(Report,Native with {Title="增值税 - 第2章 - Google Chrome"})); }
    [TestMethod] public void BlockCommandPreservesFullFragmentRoute()
    {
        var tracker=new BrowserActivityTracker(_clock,Options); var report=Report with {Url="https://example.com/app#/game"}; tracker.Accept(report,Native); var current=tracker.Enrich(Native);
        tracker.ObserveEscalation(current,new(InterventionLevel.Block,true,false,current,TimeSpan.FromSeconds(90),TimeSpan.FromSeconds(90),TimeSpan.FromSeconds(90),TimeSpan.Zero,TimeSpan.Zero,1,1),"税法");
        Assert.AreEqual(report.Url,tracker.GetCommand(report.ClientId).Url);
    }
    [TestMethod] public void FocusResetRevokesAnUndeliveredBlock()
    {
        var tracker=new BrowserActivityTracker(_clock,Options); tracker.Accept(Report,Native); var current=tracker.Enrich(Native);
        var blocked=new EscalationSnapshot(InterventionLevel.Block,true,false,current,TimeSpan.FromSeconds(90),TimeSpan.FromSeconds(90),TimeSpan.FromSeconds(90),TimeSpan.Zero,TimeSpan.Zero,1,1);
        tracker.ObserveEscalation(current,blocked,"税法"); tracker.ObserveEscalation(current,blocked with {Level=InterventionLevel.None,IsDistracting=false},"税法");
        Assert.AreEqual("none",tracker.GetCommand(Report.ClientId).Action);
    }
    [TestMethod] public void BackgroundWrongProcessAndWrongTitleCannotSupplyUrl()
    { var tracker=new BrowserActivityTracker(_clock,Options); Assert.IsFalse(tracker.Accept(Report with {Focused=false},Native)); Assert.IsFalse(tracker.Accept(Report with {Process="msedge.exe"},Native)); Assert.IsFalse(tracker.Accept(Report with {Title="游戏"},Native)); Assert.IsNull(tracker.Enrich(Native).Url); }
    [TestMethod] public void ReportExpiresAndNativeWindowChangeInvalidatesAssociation()
    { var tracker=new BrowserActivityTracker(_clock,Options); tracker.Accept(Report,Native); _clock.Advance(6); Assert.IsNull(tracker.Enrich(Native).Url);
      tracker.Accept(Report with { Sequence=2 },Native); Assert.IsNull(tracker.Enrich(Native with {Handle=99}).Url); Assert.IsNull(tracker.Enrich(Native).Url); }
    [TestMethod] public void OutOfOrderReportCannotReplaceNewTabAndFragmentsArePreserved()
    { var tracker=new BrowserActivityTracker(_clock,Options); tracker.Accept(Report with {Sequence=2,Url="https://www.bilibili.com/video/game#part"},Native);
      Assert.IsFalse(tracker.Accept(Report,Native)); Assert.AreEqual("https://www.bilibili.com/video/game#part",tracker.Enrich(Native).Url); }
    [TestMethod] public void UnsupportedUrlsAndCredentialsAreRejected()
    { var tracker=new BrowserActivityTracker(_clock,Options); foreach(var url in new[]{"file:///C:/test.txt","chrome://settings","https://user:pass@example.com/","javascript:alert(1)"}) Assert.IsFalse(tracker.Accept(Report with {Url=url},Native)); }
    [TestMethod] public void BlockCommandSurvivesOverlayFocusButExpiresAndClearRemovesIt()
    { var tracker=new BrowserActivityTracker(_clock,Options); tracker.Accept(Report,Native); var enriched=tracker.Enrich(Native);
      tracker.ObserveEscalation(enriched,new(InterventionLevel.Block,true,false,enriched,TimeSpan.FromSeconds(90),TimeSpan.FromSeconds(90),TimeSpan.FromSeconds(90),TimeSpan.Zero,TimeSpan.FromSeconds(60),1,1),"税法");
      tracker.Enrich(new(9,1,"StudyGuardian.App.exe","拦截")); Assert.AreEqual("block",tracker.GetCommand(Report.ClientId).Action); Assert.AreEqual(5,tracker.GetCommand(Report.ClientId).TabId);
      Assert.AreEqual("none",tracker.GetCommand("different").Action); _clock.Advance(31); Assert.AreEqual("none",tracker.GetCommand(Report.ClientId).Action); tracker.Clear(); Assert.IsNull(tracker.Enrich(Native).Url); }
}
