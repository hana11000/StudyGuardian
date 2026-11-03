using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;

namespace StudyGuardian.Core.Tests;

[TestClass]
public sealed class ExternalDecisionTests
{
    private readonly FakeClock _clock = new();
    private static readonly WindowSnapshot Window = new(1, 2, "chrome.exe", "税法");
    private StudySession Create()
    {
        var session = new StudySession(_clock, new(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100)), ["taskmgr"], 999);
        session.Start("s", new("学习", TimeSpan.FromMinutes(25), SessionStrictness.Standard, []), Window, TimeSpan.Zero);
        return session;
    }
    [TestMethod]
    public void OnlyRequestsAfterDwellAndSplitsWhenAiArrives()
    {
        var session = Create(); Assert.IsNull(session.PendingDecision);
        _clock.Advance(5); session.Observe(Window, TimeSpan.Zero);
        var ticket = session.PendingDecision; Assert.IsNotNull(ticket);
        _clock.Advance(1);
        var closed = session.ApplyExternalDecision(ticket, new(StudyVerdict.OnTask, DecisionSource.Llm), Window, TimeSpan.Zero);
        Assert.AreEqual(StudyVerdict.OnTask, session.Snapshot.Decision.Verdict);
        Assert.AreEqual(6, closed.Sum(s => s.Duration.TotalSeconds));
        Assert.IsNull(session.PendingDecision);
    }
    [TestMethod]
    public void LeavingAndReturningToSameWindowRejectsOldReply()
    {
        var session = Create(); _clock.Advance(5); session.Observe(Window, TimeSpan.Zero);
        var ticket = session.PendingDecision; Assert.IsNotNull(ticket);
        session.Observe(Window with { Title = "游戏" }, TimeSpan.Zero);
        session.Observe(Window, TimeSpan.Zero); _clock.Advance(5); session.Observe(Window, TimeSpan.Zero);
        session.ApplyExternalDecision(ticket, new(StudyVerdict.Distracted, DecisionSource.Llm), Window, TimeSpan.Zero);
        Assert.AreEqual(StudyVerdict.Unknown, session.Snapshot.Decision.Verdict);
    }
    [TestMethod]
    public void PauseAndNewSessionInvalidateOldReply()
    {
        var session = Create(); _clock.Advance(5); session.Observe(Window, TimeSpan.Zero);
        var ticket = session.PendingDecision; Assert.IsNotNull(ticket);
        session.SetManualPause(true, Window, TimeSpan.Zero);
        session.ApplyExternalDecision(ticket, new(StudyVerdict.Distracted, DecisionSource.Llm), Window, TimeSpan.Zero);
        Assert.AreEqual(StudyVerdict.Neutral, session.Snapshot.Decision.Verdict);
        session.Stop(); session.Start("next", new("新目标", TimeSpan.FromMinutes(25), SessionStrictness.Standard, []), Window, TimeSpan.Zero);
        _clock.Advance(5); session.Observe(Window, TimeSpan.Zero);
        session.ApplyExternalDecision(ticket, new(StudyVerdict.Distracted, DecisionSource.Llm), Window, TimeSpan.Zero);
        Assert.AreEqual(StudyVerdict.Unknown, session.Snapshot.Decision.Verdict);
    }

    [TestMethod]
    public void RetryFallbackKeepsVisitDwellAndRejectsThePreviousTicket()
    {
        var session = Create(); _clock.Advance(5); session.Observe(Window, TimeSpan.Zero);
        var old = session.PendingDecision!;
        session.ApplyExternalDecision(old, new(StudyVerdict.Supportive, DecisionSource.Fallback), Window, TimeSpan.Zero);
        _clock.Advance(30);
        var closed = session.RetryFallbackDecision(Window, TimeSpan.Zero);
        Assert.AreEqual(30, closed.Sum(s => s.Duration.TotalSeconds));
        Assert.IsNotNull(session.PendingDecision);
        Assert.AreEqual(35, session.PendingDecision.DwellSeconds);
        session.ApplyExternalDecision(old, new(StudyVerdict.Distracted, DecisionSource.Llm), Window, TimeSpan.Zero);
        Assert.AreEqual(StudyVerdict.Unknown, session.Snapshot.Decision.Verdict);
        session.ApplyExternalDecision(session.PendingDecision!, new(StudyVerdict.OnTask, DecisionSource.Llm), Window, TimeSpan.Zero);
        Assert.AreEqual(StudyVerdict.OnTask, session.Snapshot.Decision.Verdict);
    }

    [TestMethod]
    public void RetryDoesNotBypassNewWindowDwellOrPause()
    {
        var session = Create(); _clock.Advance(5); session.Observe(Window, TimeSpan.Zero);
        session.ApplyExternalDecision(session.PendingDecision!, new(StudyVerdict.Supportive, DecisionSource.Fallback), Window, TimeSpan.Zero);
        session.RetryFallbackDecision(Window with { Title = "新窗口" }, TimeSpan.Zero);
        Assert.IsNull(session.PendingDecision);
        Assert.AreEqual(TimeSpan.FromSeconds(5), session.DecisionDwellRemaining);
        session.SetManualPause(true, Window, TimeSpan.Zero);
        session.RetryFallbackDecision(Window, TimeSpan.Zero);
        Assert.IsNull(session.PendingDecision);
        Assert.AreEqual(StudyVerdict.Neutral, session.Snapshot.Decision.Verdict);
    }
}
