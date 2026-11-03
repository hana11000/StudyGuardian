using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;

namespace StudyGuardian.Core.Tests;

[TestClass]
public sealed class StudySessionTests
{
    private FakeClock _clock = null!;
    private StudySession _session = null!;
    private static readonly WindowSnapshot Study = new(1, 10, "Notion.exe", "增值税");
    private static readonly WindowSnapshot Help = new(2, 11, "calc.exe", "计算器");
    private static readonly WindowSnapshot Game = new(3, 12, "game.exe", "游戏");
    private static readonly WindowSnapshot System = new(4, 13, "taskmgr.exe", "任务管理器");
    private static StudySessionOptions Options(double seconds = 1500) => new("学习增值税", TimeSpan.FromSeconds(seconds), SessionStrictness.Standard,
        [new("study", RuleKind.Allow, RuleMatchType.Process, "Notion"), new("help", RuleKind.Supportive, RuleMatchType.Process, "calc"),
         new("game", RuleKind.Block, RuleMatchType.Process, "game")]);

    [TestInitialize]
    public void SetUp()
    {
        _clock = new();
        _session = new(_clock, new(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100)), ["taskmgr.exe"], 999);
    }

    [TestMethod]
    public void DwellBoundaryAndSwitchResetDecision()
    {
        _session.Start("s", Options(), Study, TimeSpan.Zero);
        Assert.IsTrue(_session.Snapshot.IsRunning);
        _clock.Advance(4);
        _session.Observe(Study, TimeSpan.Zero);
        Assert.AreEqual(StudyVerdict.Unknown, _session.Snapshot.Decision.Verdict);
        _clock.Advance(1);
        _session.Observe(Study, TimeSpan.Zero);
        Assert.AreEqual(StudyVerdict.OnTask, _session.Snapshot.Decision.Verdict);
        _session.Observe(Game, TimeSpan.Zero);
        Assert.AreEqual(StudyVerdict.Unknown, _session.Snapshot.Decision.Verdict);
        _clock.Advance(5);
        _session.Observe(Game, TimeSpan.Zero);
        Assert.AreEqual(StudyVerdict.Distracted, _session.Snapshot.Decision.Verdict);
    }

    [TestMethod]
    public void SystemWindowImmediatelyPausesCountdown()
    {
        _session.Start("s", Options(60), Study, TimeSpan.Zero);
        _clock.Advance(10);
        _session.Observe(System, TimeSpan.Zero);
        Assert.AreEqual(StudyVerdict.Neutral, _session.Snapshot.Decision.Verdict);
        _clock.Advance(20);
        _session.Observe(System, TimeSpan.Zero);
        Assert.AreEqual(50, _session.Snapshot.Remaining.TotalSeconds);
    }

    [TestMethod]
    public void CountdownCompletesAndFreezesAtZero()
    {
        _session.Start("s", Options(60), Study, TimeSpan.Zero);
        _clock.Advance(60);
        var segments = _session.Observe(Study, TimeSpan.Zero);
        Assert.IsFalse(_session.Snapshot.IsRunning);
        Assert.IsTrue(_session.Snapshot.IsCompleted);
        Assert.AreEqual(TimeSpan.Zero, _session.Snapshot.Remaining);
        Assert.AreEqual(60, segments.Sum(s => s.ActiveSeconds));
        var ended = _session.EndedAt;
        _clock.Advance(50);
        Assert.HasCount(0, _session.Stop());
        Assert.AreEqual(ended, _session.EndedAt);
    }

    [TestMethod]
    public void ManualPauseFreezesTimeAndResumesSameSession()
    {
        _session.Start("s", Options(60), Study, TimeSpan.Zero);
        _clock.Advance(10);
        _session.SetManualPause(true, Study, TimeSpan.Zero);
        _clock.Advance(30);
        _session.Observe(Game, TimeSpan.Zero);
        Assert.IsTrue(_session.Snapshot.IsManuallyPaused);
        Assert.AreEqual(50, _session.Snapshot.Remaining.TotalSeconds);
        _session.SetManualPause(false, Study, TimeSpan.Zero);
        _clock.Advance(5);
        _session.Observe(Study, TimeSpan.Zero);
        Assert.AreEqual(45, _session.Snapshot.Remaining.TotalSeconds);
        Assert.IsFalse(_session.Snapshot.IsManuallyPaused);
        Assert.AreEqual(StudyVerdict.OnTask, _session.Snapshot.Decision.Verdict);
    }

    [TestMethod]
    public void UnlockDoesNotClearManualPause()
    {
        _session.Start("s", Options(60), Study, TimeSpan.Zero);
        _session.SetManualPause(true, Study, TimeSpan.Zero);
        _session.SetSystemPause(PauseReason.Locked, Study, TimeSpan.Zero);
        _clock.Advance(20);
        _session.SetSystemPause(null, Study, TimeSpan.Zero);
        _clock.Advance(20);
        _session.Observe(Study, TimeSpan.Zero);
        Assert.IsTrue(_session.Snapshot.IsManuallyPaused);
        Assert.AreEqual(60, _session.Snapshot.Remaining.TotalSeconds);
    }

    [TestMethod]
    public void ManualResumeDoesNotClearSystemPause()
    {
        _session.Start("s", Options(60), Study, TimeSpan.Zero);
        _session.SetManualPause(true, Study, TimeSpan.Zero);
        _session.SetSystemPause(PauseReason.Suspended, Study, TimeSpan.Zero);
        _session.SetManualPause(false, Study, TimeSpan.Zero);
        _clock.Advance(50);
        _session.Observe(Study, TimeSpan.Zero);
        Assert.AreEqual(PauseReason.Suspended, _session.Snapshot.PauseReason);
        Assert.AreEqual(60, _session.Snapshot.Remaining.TotalSeconds);
    }

    [TestMethod]
    public void FocusRateUsesOnlyClassifiedActiveTimeAndCheckpointsDoNotDoubleCount()
    {
        _session.Start("s", Options(), Study, TimeSpan.Zero);
        _clock.Advance(5); _session.Observe(Study, TimeSpan.Zero);
        _clock.Advance(60); _session.Observe(Study, TimeSpan.Zero);
        _session.Checkpoint(); _session.Checkpoint();
        Assert.AreEqual(1.0, _session.Snapshot.FocusRate!.Value, 0.000001);
        _session.Observe(Help, TimeSpan.Zero);
        _clock.Advance(5); _session.Observe(Help, TimeSpan.Zero);
        _clock.Advance(20); _session.Observe(Game, TimeSpan.Zero);
        _clock.Advance(5); _session.Observe(Game, TimeSpan.Zero);
        _clock.Advance(20); _session.Observe(System, TimeSpan.Zero);
        _clock.Advance(100); _session.Observe(new(5, 14, "unknown.exe", "未知"), TimeSpan.Zero);
        _clock.Advance(40); _session.Observe(new(5, 14, "unknown.exe", "未知"), TimeSpan.Zero);
        Assert.AreEqual(0.6, _session.Snapshot.FocusRate!.Value, 0.000001);
        _session.Stop();
        Assert.AreEqual(0.6, _session.Snapshot.FocusRate!.Value, 0.000001);
    }

    [TestMethod]
    public void UnknownOnlyHasNoFocusRate()
    {
        _session.Start("s", Options(), new(5, 14, "unknown.exe", "未知"), TimeSpan.Zero);
        _clock.Advance(20);
        _session.Observe(new(5, 14, "unknown.exe", "未知"), TimeSpan.Zero);
        Assert.IsNull(_session.Snapshot.FocusRate);
        Assert.AreEqual(1480, _session.Snapshot.Remaining.TotalSeconds);
    }

    [TestMethod]
    public void LateIdleSampleSplitsAtThresholdAndRearmsDwellOnResume()
    {
        _session.Start("s", Options(600), Study, TimeSpan.Zero);
        _clock.Advance(5); _session.Observe(Study, TimeSpan.FromSeconds(5));
        _clock.Advance(305);
        var segments = _session.Observe(Study, TimeSpan.FromSeconds(310));
        Assert.AreEqual(300, _session.Snapshot.Remaining.TotalSeconds);
        Assert.AreEqual(PauseReason.Away, _session.Snapshot.PauseReason);
        Assert.AreEqual(295, segments.Sum(s => s.ActiveSeconds));
        _clock.Advance(10); _session.Observe(Study, TimeSpan.Zero);
        Assert.AreEqual(StudyVerdict.Unknown, _session.Snapshot.Decision.Verdict);
        _clock.Advance(5); _session.Observe(Study, TimeSpan.Zero);
        Assert.AreEqual(StudyVerdict.OnTask, _session.Snapshot.Decision.Verdict);
        Assert.AreEqual(295, _session.Snapshot.Remaining.TotalSeconds);
    }

    [TestMethod]
    public void WallClockChangeDoesNotChangeCountdown()
    {
        _session.Start("s", Options(60), Study, TimeSpan.Zero);
        _clock.Advance(10); _clock.UtcNow = _clock.UtcNow.AddHours(-2);
        _session.Observe(Study, TimeSpan.Zero);
        Assert.AreEqual(50, _session.Snapshot.Remaining.TotalSeconds);
    }

    [TestMethod]
    public void RestartResetsPriorSessionStatistics()
    {
        _session.Start("s", Options(), Study, TimeSpan.Zero);
        _clock.Advance(5); _session.Observe(Study, TimeSpan.Zero);
        _clock.Advance(20); _session.Stop();
        _session.Start("s2", Options(90), Game, TimeSpan.Zero);
        Assert.IsNull(_session.Snapshot.FocusRate);
        Assert.AreEqual(90, _session.Snapshot.Remaining.TotalSeconds);
    }

    [TestMethod]
    public void InvalidDurationOrGoalCannotStart()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _session.Start("s", Options() with { Goal = " " }, Study, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _session.Start("s", Options(0), Study, TimeSpan.Zero));
    }

    [TestMethod]
    public void DelayedSampleEndsAtExactActiveBudgetInsteadOfRecordingTheDelay()
    {
        var start = _clock.UtcNow;
        _session.Start("s", Options(60), Study, TimeSpan.Zero);
        _clock.Advance(5); _session.Observe(Study, TimeSpan.Zero);
        _clock.Advance(54); _session.Observe(Study, TimeSpan.Zero);
        _session.Checkpoint();
        _clock.Advance(541);
        var last = _session.Observe(Study, TimeSpan.Zero);
        Assert.AreEqual(55, last.Sum(s => s.ActiveSeconds));
        Assert.AreEqual(start.AddSeconds(60), _session.EndedAt);
        Assert.AreEqual(start.AddSeconds(60), last.Last().EndedAt);
        Assert.IsTrue(_session.Snapshot.IsCompleted);
    }

    [TestMethod]
    public void ExactCompletionKeepsTheIdlePauseBeforeTheBudgetBoundary()
    {
        var start = _clock.UtcNow;
        _session.Start("s", Options(600), Study, TimeSpan.Zero);
        _clock.Advance(310);
        var activeBeforeAway = _session.Observe(Study, TimeSpan.FromSeconds(310));
        Assert.AreEqual(300, activeBeforeAway.Sum(s => s.ActiveSeconds));
        _clock.Advance(10);
        var paused = _session.Observe(Study, TimeSpan.Zero);
        Assert.AreEqual(20, paused.Sum(s => s.Duration.TotalSeconds));
        _clock.Advance(400);
        var final = _session.Observe(Study, TimeSpan.Zero);
        Assert.AreEqual(300, final.Sum(s => s.ActiveSeconds));
        Assert.AreEqual(start.AddSeconds(620), _session.EndedAt);
        Assert.AreEqual(start.AddSeconds(620), final.Last().EndedAt);
    }
}
