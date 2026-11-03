using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;

[assembly: DoNotParallelize]

namespace StudyGuardian.Core.Tests;

[TestClass]
public sealed class ActivityRecorderTests
{
    private static readonly WindowSnapshot Notes = new(101, 10, "Notion.exe", "第3章 增值税");
    private static readonly WindowSnapshot Browser = new(102, 11, "chrome.exe", "税法讲解");
    private readonly FakeClock _clock = new();

    private ActivityRecorder Create() => new(_clock, TimeSpan.FromSeconds(300), ["explorer.exe", "taskmgr.exe"], 999);

    [TestMethod]
    public void SwitchingWindowsClosesThePreviousWindowWithItsDwellTime()
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.Zero);
        _clock.Advance(12);
        var segments = recorder.Observe(Browser, TimeSpan.Zero);
        Assert.HasCount(1, segments);
        Assert.AreEqual(Notes, segments[0].Window);
        Assert.AreEqual(12, segments[0].Duration.TotalSeconds);
        Assert.AreEqual(12, recorder.Snapshot.ActiveDuration.TotalSeconds);
        Assert.AreEqual(Browser, recorder.Snapshot.Window);
    }

    [TestMethod]
    public void DuplicateWindowEventsDoNotResetDwellOrCreateExtraSegments()
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.Zero);
        _clock.Advance(7);
        Assert.IsEmpty(recorder.Observe(Notes, TimeSpan.Zero));
        _clock.Advance(5);
        Assert.AreEqual(12, recorder.Stop()!.Duration.TotalSeconds);
    }

    [TestMethod]
    public void ShortAltTabIsRecordedEvenBeforeLaterDecisionDebounce()
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.Zero);
        _clock.Advance(0.5);
        Assert.AreEqual(0.5, recorder.Observe(Browser, TimeSpan.Zero)[0].Duration.TotalSeconds);
    }

    [TestMethod]
    public void StopFlushesLastSegmentAndIsIdempotent()
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.Zero);
        _clock.Advance(25);
        Assert.AreEqual(25, recorder.Stop()!.Duration.TotalSeconds);
        Assert.IsFalse(recorder.Snapshot.IsRunning);
        Assert.IsNull(recorder.Stop());
        _clock.Advance(100);
        Assert.AreEqual(25, recorder.Snapshot.ActiveDuration.TotalSeconds);
    }

    [TestMethod]
    public void CheckpointUpdatesTheSameSegmentWithoutDoubleCounting()
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.Zero);
        _clock.Advance(15);
        recorder.Observe(Notes, TimeSpan.Zero);
        var checkpoint = recorder.Checkpoint()!;
        _clock.Advance(10);
        var closed = recorder.Stop()!;
        Assert.AreEqual(checkpoint.Id, closed.Id);
        Assert.AreEqual(25, closed.Duration.TotalSeconds);
        Assert.AreEqual(25, recorder.Snapshot.ActiveDuration.TotalSeconds);
    }

    [TestMethod]
    public void ReadingWithoutInputBelowFiveMinutesStillCountsAsActive()
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.Zero);
        _clock.Advance(299);
        Assert.IsEmpty(recorder.Observe(Notes, TimeSpan.FromSeconds(299)));
        Assert.AreEqual(PauseReason.None, recorder.Snapshot.PauseReason);
        Assert.AreEqual(299, recorder.Snapshot.ActiveDuration.TotalSeconds);
    }

    [TestMethod]
    public void DelayedIdleSampleSplitsAtTheThresholdInsteadOfTheSampleTime()
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.Zero);
        _clock.Advance(301);
        var segments = recorder.Observe(Notes, TimeSpan.FromSeconds(301));
        Assert.AreEqual(300, segments[0].Duration.TotalSeconds);
        Assert.AreEqual(PauseReason.Away, recorder.Snapshot.PauseReason);
        Assert.AreEqual(1, recorder.Checkpoint()!.Duration.TotalSeconds);
        Assert.AreEqual(300, recorder.Snapshot.ActiveDuration.TotalSeconds);
    }

    [TestMethod]
    public void ExactIdleThresholdStartsAwayState()
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.Zero);
        _clock.Advance(300);
        recorder.Observe(Notes, TimeSpan.FromSeconds(300));
        Assert.AreEqual(PauseReason.Away, recorder.Snapshot.PauseReason);
        Assert.AreEqual(300, recorder.Snapshot.ActiveDuration.TotalSeconds);
    }

    [TestMethod]
    public void ReturningFromAwayResumesAtLastInputTime()
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.Zero);
        _clock.Advance(301);
        recorder.Observe(Notes, TimeSpan.FromSeconds(301));
        _clock.Advance(20);
        var away = recorder.Observe(Notes, TimeSpan.FromSeconds(0.5)).Single();
        Assert.AreEqual(PauseReason.Away, away.PauseReason);
        Assert.AreEqual(20.5, away.Duration.TotalSeconds);
        Assert.AreEqual(300.5, recorder.Snapshot.ActiveDuration.TotalSeconds);
    }

    [TestMethod]
    public void StartingWhileAlreadyIdleNeverCountsAnEarlierUnobservedPeriod()
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.FromSeconds(900));
        _clock.Advance(10);
        Assert.AreEqual(0, recorder.Snapshot.ActiveDuration.TotalSeconds);
        Assert.AreEqual(10, recorder.Stop()!.Duration.TotalSeconds);
    }

    [TestMethod]
    [DataRow("TASKMGR.EXE", 9, true, PauseReason.SystemWindow)]
    [DataRow("explorer", 9, true, PauseReason.SystemWindow)]
    [DataRow("renamed-app.exe", 999, true, PauseReason.SystemWindow)]
    [DataRow("", 0, false, PauseReason.Unavailable)]
    public void ProtectedOwnAndUnreadableWindowsPauseActiveTime(string name, int pid, bool accessible, PauseReason expected)
    {
        var recorder = Create();
        recorder.Start("session", new(1, pid, name, "", accessible), TimeSpan.Zero);
        _clock.Advance(20);
        Assert.AreEqual(expected, recorder.Snapshot.PauseReason);
        Assert.AreEqual(0, recorder.Snapshot.ActiveDuration.TotalSeconds);
        Assert.AreEqual(20, recorder.Stop()!.Duration.TotalSeconds);
    }

    [TestMethod]
    public void TitleChangeInSameWindowCreatesANewSegment()
    {
        var recorder = Create();
        recorder.Start("session", Browser, TimeSpan.Zero);
        _clock.Advance(10);
        var nextTab = Browser with { Title = "另一个页面" };
        Assert.AreEqual("税法讲解", recorder.Observe(nextTab, TimeSpan.Zero).Single().Window.Title);
        Assert.AreEqual("另一个页面", recorder.Snapshot.Window!.Title);
    }

    [TestMethod]
    [DataRow(PauseReason.Locked)]
    [DataRow(PauseReason.Suspended)]
    public void SuspensionDoesNotCountSleepOrLetWindowEventsResumeIt(PauseReason reason)
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.Zero);
        _clock.Advance(10);
        recorder.Suspend(reason);
        _clock.Advance(3600);
        Assert.IsEmpty(recorder.Observe(Browser, TimeSpan.Zero));
        Assert.AreEqual(reason, recorder.Snapshot.PauseReason);
        Assert.AreEqual(10, recorder.Snapshot.ActiveDuration.TotalSeconds);
        recorder.Resume(Browser, TimeSpan.Zero);
        _clock.Advance(5);
        Assert.AreEqual(15, recorder.Snapshot.ActiveDuration.TotalSeconds);
    }

    [TestMethod]
    public void WallClockJumpDoesNotCorruptDurationsOrMakeEndPrecedeStart()
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.Zero);
        _clock.Advance(12);
        _clock.UtcNow = _clock.UtcNow.AddHours(-1);
        var segment = recorder.Stop()!;
        Assert.AreEqual(12, segment.Duration.TotalSeconds);
        Assert.AreEqual(TimeSpan.FromSeconds(12), segment.EndedAt - segment.StartedAt);
    }

    [TestMethod]
    public void RestartUsesANewSessionWithoutCarryingOldTotals()
    {
        var recorder = Create();
        recorder.Start("first", Notes, TimeSpan.Zero);
        _clock.Advance(10);
        recorder.Stop();
        recorder.Start("second", Browser, TimeSpan.Zero);
        _clock.Advance(2);
        var segment = recorder.Stop()!;
        Assert.AreEqual("second", segment.SessionId);
        Assert.AreEqual(2, recorder.Snapshot.ActiveDuration.TotalSeconds);
    }

    [TestMethod]
    public void ZeroLengthTransitionsDoNotProduceRows()
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.Zero);
        Assert.IsEmpty(recorder.Observe(Browser, TimeSpan.Zero));
        Assert.IsNull(recorder.Stop());
    }

    [TestMethod]
    public void CheckpointOnlyPersistsTimeThatHasAlreadyBeenClassified()
    {
        var recorder = Create();
        recorder.Start("session", Notes, TimeSpan.Zero);
        _clock.Advance(299.999);
        recorder.Observe(Notes, TimeSpan.FromSeconds(299.999));
        _clock.Advance(0.002);
        Assert.AreEqual(299.999, recorder.Checkpoint()!.Duration.TotalSeconds, 0.0000001);
    }

    [TestMethod]
    public void LogicalSessionEndIsFrozenBeforeWallClockJumpAndSaveDelay()
    {
        var recorder = Create();
        var start = _clock.UtcNow;
        recorder.Start("session", Notes, TimeSpan.Zero);
        _clock.Advance(12);
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        recorder.Stop();
        _clock.Advance(5);
        Assert.AreEqual(start.AddSeconds(12), recorder.EndedAt);
    }
}
