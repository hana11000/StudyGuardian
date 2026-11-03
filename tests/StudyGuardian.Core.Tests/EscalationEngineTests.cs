using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;

namespace StudyGuardian.Core.Tests;

[TestClass]
public sealed class EscalationEngineTests
{
    private FakeClock _clock = null!;
    private EscalationEngine _engine = null!;
    private static readonly WindowSnapshot Game = new(1, 2, "game.exe", "游戏");
    private static readonly WindowSnapshot Study = new(2, 3, "Notion.exe", "学习");
    private static readonly RuleDecision Distracted = new(StudyVerdict.Distracted, DecisionSource.Rule);
    private static readonly RuleDecision Focused = new(StudyVerdict.OnTask, DecisionSource.Rule);
    internal static InterventionOptions Options => new()
    {
        ReminderSeconds = 10, WarningSeconds = 40, BlockSeconds = 90, GentleMultiplier = 1.5, StrictMultiplier = .5,
        FocusResetSeconds = 20, RollingWindowSeconds = 1800, AccelerationAfterSeconds = 300, BlockLockSeconds = 60,
        FrameThicknessPixels = 4, OverlayOpacity = .9, SoundEnabled = true
    };
    [TestInitialize] public void SetUp()
    { _clock = new(); _engine = new(_clock, Options, SessionStrictness.Standard, TimeSpan.FromMinutes(5)); }
    private ActivitySegment Segment(double seconds, WindowSnapshot? window = null, RuleDecision? decision = null, PauseReason pause = PauseReason.None)
    {
        var start = _clock.UtcNow; _clock.Advance(seconds);
        return new(Guid.NewGuid().ToString("N"), "s", window ?? Game, start, _clock.UtcNow, TimeSpan.FromSeconds(seconds), pause, decision ?? Distracted);
    }
    private EscalationSnapshot Add(double seconds, WindowSnapshot? window = null, RuleDecision? decision = null, PauseReason pause = PauseReason.None) =>
        _engine.Record(Segment(seconds, window, decision, pause));

    [TestMethod]
    public void ExplicitResetClearsCurrentBlockButPreservesHistory()
    {
        Add(100); _engine.ResetCurrentEpisode();
        Assert.AreEqual(InterventionLevel.None,_engine.Snapshot.Level); Assert.IsFalse(_engine.Snapshot.IsDistracting);
        Assert.AreEqual(100,_engine.Snapshot.TotalDistraction.TotalSeconds); Assert.AreEqual(1,_engine.Snapshot.BlockCount);
        Assert.AreEqual(InterventionLevel.Grace,Add(1).Level);
    }
    [TestMethod]
    public void StandardBoundariesAndCooldownUseInjectedClock()
    {
        Assert.AreEqual(InterventionLevel.Grace, Add(9).Level);
        Assert.AreEqual(InterventionLevel.Reminder, Add(1).Level);
        Assert.AreEqual(InterventionLevel.Reminder, Add(29).Level);
        Assert.AreEqual(InterventionLevel.Warning, Add(1).Level);
        Assert.AreEqual(InterventionLevel.Warning, Add(49).Level);
        Assert.AreEqual(InterventionLevel.Block, Add(1).Level);
        Assert.AreEqual(60, _engine.Snapshot.LockRemaining.TotalSeconds);
        _clock.Advance(59); Assert.AreEqual(1, _engine.Snapshot.LockRemaining.TotalSeconds);
        _clock.Advance(1); Assert.AreEqual(TimeSpan.Zero, _engine.Snapshot.LockRemaining);
        Assert.AreEqual(1, _engine.Snapshot.BlockCount);
    }
    [TestMethod]
    public void ContinuousFocusClearsLevelButKeepsTotal()
    {
        Add(40); Assert.AreEqual(InterventionLevel.Warning, Add(19, Study, Focused).Level);
        Assert.AreEqual(InterventionLevel.None, Add(1, Study, Focused).Level);
        Assert.AreEqual(40, _engine.Snapshot.TotalDistraction.TotalSeconds);
        Assert.AreEqual(InterventionLevel.Grace, Add(1).Level);
    }
    [TestMethod]
    public void PausedTimeDoesNotEscalateOrCompleteFocusReset()
    {
        Add(10); Add(19, Study, Focused);
        Add(600, Study, RuleDecision.Neutral, PauseReason.Locked);
        Assert.AreEqual(InterventionLevel.Reminder, Add(1, Study, Focused).Level);
        Assert.AreEqual(10, _engine.Snapshot.TotalDistraction.TotalSeconds);
        Assert.AreEqual(InterventionLevel.None, Add(19, Study, Focused).Level);
    }
    [TestMethod]
    public void SwitchingDistractingWindowsKeepsEpisode()
    {
        Add(20);
        Assert.AreEqual(InterventionLevel.Warning, Add(20, Game with { Title = "另一个游戏" }).Level);
        Assert.AreEqual(1, _engine.Snapshot.EpisodeId);
    }
    [TestMethod]
    public void FallbackUncertainAndUnknownCannotEscalate()
    {
        foreach (var decision in new[] { new RuleDecision(StudyVerdict.Supportive, DecisionSource.Fallback),
            new RuleDecision(StudyVerdict.Distracted, DecisionSource.Fallback), new RuleDecision(StudyVerdict.Uncertain, DecisionSource.Llm), RuleDecision.Unknown })
            Assert.AreEqual(InterventionLevel.None, Add(1000, decision: decision).Level);
        Assert.AreEqual(TimeSpan.Zero, _engine.Snapshot.TotalDistraction);
    }
    [TestMethod]
    public void SupportiveLimitStartsAtReminderAndCountsOnlyOvertime()
    {
        var supportive = new RuleDecision(StudyVerdict.Supportive, DecisionSource.Rule);
        Assert.AreEqual(InterventionLevel.None, Add(299, Study, supportive).Level);
        Assert.AreEqual(InterventionLevel.Reminder, Add(1, Study, supportive).Level);
        Assert.IsTrue(_engine.Snapshot.SupportiveOvertime);
        Assert.AreEqual(0, _engine.Snapshot.TotalDistraction.TotalSeconds);
        Assert.AreEqual(InterventionLevel.Warning, Add(30, Study, supportive).Level);
        Assert.AreEqual(30, _engine.Snapshot.TotalDistraction.TotalSeconds);
    }
    [TestMethod]
    public void ChangingSupportiveWindowRestartsItsAllowance()
    {
        var supportive = new RuleDecision(StudyVerdict.Supportive, DecisionSource.Rule);
        Add(299, Study, supportive);
        Assert.AreEqual(InterventionLevel.None, Add(299, Study with { Title = "另一份资料" }, supportive).Level);
    }
    [TestMethod]
    public void RollingAccelerationAppliesToNextEpisodeAndExpires()
    {
        Add(301); Add(20, Study, Focused);
        Assert.AreEqual(InterventionLevel.Warning, Add(1).Level);
        Assert.AreEqual(302, _engine.Snapshot.TotalDistraction.TotalSeconds);
        Add(1800, decision: RuleDecision.Neutral, pause: PauseReason.Manual);
        Add(20, Study, Focused);
        Assert.AreEqual(InterventionLevel.Grace, Add(1).Level);
        Assert.AreEqual(1, _engine.Snapshot.RollingDistraction.TotalSeconds);
    }
    [TestMethod]
    public void RepeatedCheckpointAndFinalSegmentAreNotDoubleCounted()
    {
        var segment = Segment(10); _engine.Record(segment); _engine.Record(segment);
        _clock.Advance(30); var final = segment with { Duration = TimeSpan.FromSeconds(40), EndedAt = segment.StartedAt.AddSeconds(40) };
        _engine.Record(final); _engine.Record(final); _engine.Record(segment);
        Assert.AreEqual(InterventionLevel.Warning, _engine.Snapshot.Level);
        Assert.AreEqual(40, _engine.Snapshot.TotalDistraction.TotalSeconds);
    }
    [TestMethod]
    public void StrictnessScalesThresholds()
    {
        _engine = new(_clock, Options, SessionStrictness.Strict, TimeSpan.FromMinutes(5));
        Assert.AreEqual(InterventionLevel.Block, Add(45).Level);
        _engine = new(_clock, Options, SessionStrictness.Gentle, TimeSpan.FromMinutes(5));
        Assert.AreEqual(InterventionLevel.Grace, Add(10).Level);
        Assert.AreEqual(InterventionLevel.Reminder, Add(5).Level);
    }
}
