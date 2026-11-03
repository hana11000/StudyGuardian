using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;

namespace StudyGuardian.Core.Tests;

[TestClass]
public class SessionAllowancesTests
{
    private readonly FakeClock _clock=new();
    private static SessionToolsOptions Options => new() {AppealMinutes=10,EmergencyUses=3,EmergencyMinutes=5,BreakMinutes=5,MaximumAppealCharacters=200,ReportSessionLimit=50};
    private static WindowSnapshot Game => new(1,2,"chrome.exe","视频",Url:"https://example.com/app#/game");
    [TestMethod] public void AppealIsExactObjectAndExpiresAtTenMinutes()
    {
        var service=new SessionAllowances(_clock,Options); service.GrantAppeal(Game);
        Assert.AreEqual(DecisionSource.Override,service.DecisionFor(Game)?.Source);
        Assert.IsNull(service.DecisionFor(Game with {Url="https://example.com/app#/study"}));
        _clock.Advance(599); Assert.IsNotNull(service.DecisionFor(Game)); _clock.Advance(1); Assert.IsNull(service.DecisionFor(Game));
    }
    [TestMethod] public void DesktopObjectUsesProcessAndTitleWithoutRequiringTheSameHandle()
    {
        var service=new SessionAllowances(_clock,Options); var desktop=Game with {ProcessName="reader.exe",Url=null}; service.GrantAppeal(desktop);
        Assert.IsNotNull(service.DecisionFor(desktop with {Handle=99})); Assert.IsNull(service.DecisionFor(desktop with {Title="另一本书"}));
    }
    [TestMethod] public void ThreeEmergencyPassesExpireAndResetForNewSession()
    {
        var service=new SessionAllowances(_clock,Options);
        for(var i=0;i<3;i++) {Assert.IsTrue(service.TryUseEmergency());Assert.AreEqual(2-i,service.Snapshot.EmergencyUsesRemaining);Assert.IsFalse(service.TryUseEmergency());Assert.IsNotNull(service.DecisionFor(Game));_clock.Advance(300);Assert.IsNull(service.DecisionFor(Game));}
        Assert.IsFalse(service.TryUseEmergency());service.Reset();Assert.AreEqual(3,service.Snapshot.EmergencyUsesRemaining);Assert.IsTrue(service.TryUseEmergency());
    }
    [TestMethod] public void BreakUsesInjectedTimeAndCanEndEarly()
    {
        var service=new SessionAllowances(_clock,Options); service.BeginBreak(); _clock.Advance(120); Assert.AreEqual(180,service.Snapshot.BreakRemaining.TotalSeconds);
        service.BeginBreak(); Assert.AreEqual(180,service.Snapshot.BreakRemaining.TotalSeconds);service.EndBreak();Assert.AreEqual(TimeSpan.Zero,service.Snapshot.BreakRemaining);
        service.BeginBreak();_clock.Advance(300);Assert.AreEqual(TimeSpan.Zero,service.Snapshot.BreakRemaining);
    }
    [TestMethod] public void TemporarySupportCannotEscalate()
    { Assert.IsFalse(new RuleDecision(StudyVerdict.Supportive,DecisionSource.Override).CanEnforce); }
    [TestMethod] public void SessionUsesOverrideThenReevaluatesAfterExpiryButProtectsSystemWindows()
    {
        var service=new SessionAllowances(_clock,Options);
        var session=new StudySession(_clock,new(TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(5),TimeSpan.FromMilliseconds(100)),["taskmgr.exe"],999,service.DecisionFor);
        session.Start("s",new("税法",TimeSpan.FromMinutes(50),SessionStrictness.Standard,[new("block",RuleKind.Block,RuleMatchType.Process,"chrome.exe")]),Game,TimeSpan.Zero);
        _clock.Advance(5); session.Observe(Game,TimeSpan.Zero);Assert.AreEqual(StudyVerdict.Distracted,session.Snapshot.Decision.Verdict);
        service.GrantAppeal(Game); session.Observe(Game,TimeSpan.Zero); Assert.AreEqual(DecisionSource.Override,session.Snapshot.Decision.Source);
        _clock.Advance(600); session.Observe(Game,TimeSpan.Zero);Assert.AreEqual(StudyVerdict.Unknown,session.Snapshot.Decision.Verdict);
        _clock.Advance(5);session.Observe(Game,TimeSpan.Zero);Assert.AreEqual(StudyVerdict.Distracted,session.Snapshot.Decision.Verdict);
        service.TryUseEmergency();session.Observe(Game with {ProcessName="taskmgr.exe"},TimeSpan.Zero);Assert.AreEqual(StudyVerdict.Neutral,session.Snapshot.Decision.Verdict);
    }
    [TestMethod] public void PendingAiTicketCannotReplaceTemporaryPermission()
    {
        var service=new SessionAllowances(_clock,Options);var session=new StudySession(_clock,new(TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(5),TimeSpan.FromMilliseconds(100)),[],999,service.DecisionFor);
        session.Start("s",new("税法",TimeSpan.FromMinutes(50),SessionStrictness.Standard,[]),Game,TimeSpan.Zero);_clock.Advance(5);session.Observe(Game,TimeSpan.Zero);var ticket=session.PendingDecision!;
        service.GrantAppeal(Game);session.Observe(Game,TimeSpan.Zero);session.ApplyExternalDecision(ticket,new(StudyVerdict.Distracted,DecisionSource.Llm),Game,TimeSpan.Zero);
        Assert.AreEqual(DecisionSource.Override,session.Snapshot.Decision.Source);
    }
}
