using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;

namespace StudyGuardian.Core.Tests;

[TestClass]
public class SessionReportTests
{
    private readonly FakeClock _clock=new();
    private static StoredStudySession Session => new("s","税法",new(2026,11,3,0,0,0,TimeSpan.Zero),null,SessionStrictness.Standard,"completed",EscalationEngineTests.Options,5);
    private ActivitySegment Segment(double seconds,StudyVerdict verdict,DecisionSource source=DecisionSource.Rule,PauseReason pause=PauseReason.None,string process="chrome.exe")
    { var start=_clock.UtcNow;_clock.Advance(seconds);return new(Guid.NewGuid().ToString("N"),"s",new(1,2,process,"合成窗口",Url:"https://www.bilibili.com/video/test"),start,_clock.UtcNow,TimeSpan.FromSeconds(seconds),pause,new(verdict,source)); }
    [TestMethod] public void FocusRateExcludesUnknownAndPauseAndCountsDistractionEpisodes()
    {
        var segments=new[]{Segment(120,StudyVerdict.OnTask),Segment(30,StudyVerdict.Supportive),Segment(100,StudyVerdict.Distracted),Segment(20,StudyVerdict.OnTask),Segment(10,StudyVerdict.Distracted),Segment(60,StudyVerdict.Neutral,pause:PauseReason.Manual),Segment(40,StudyVerdict.Unknown)};
        var report=SessionReportBuilder.Build(Session,segments,EscalationEngineTests.Options,5);
        Assert.AreEqual(140,report.Focused.TotalSeconds);Assert.AreEqual(30,report.Supportive.TotalSeconds);Assert.AreEqual(110,report.Distracted.TotalSeconds);
        Assert.AreEqual(.5,report.FocusRate!.Value,.0001);Assert.AreEqual(2,report.DistractionEpisodes);Assert.AreEqual(1,report.Blocks);Assert.AreEqual(60,report.Paused.TotalSeconds);Assert.AreEqual(40,report.Unclassified.TotalSeconds);
    }
    [TestMethod] public void OnlySupportiveExcessIsRedAndDuplicateCheckpointIsNotDoubleCounted()
    {
        var segment=Segment(360,StudyVerdict.Supportive);var checkpoint=segment with {Duration=TimeSpan.FromSeconds(200),EndedAt=segment.StartedAt.AddSeconds(200)};
        var report=SessionReportBuilder.Build(Session,[checkpoint,segment],EscalationEngineTests.Options,5);
        Assert.AreEqual(300,report.Supportive.TotalSeconds);Assert.AreEqual(60,report.Distracted.TotalSeconds);Assert.AreEqual(360,report.Timeline.Sum(s=>s.Duration.TotalSeconds));
        Assert.AreEqual(60,report.Sources.Single().Duration.TotalSeconds);Assert.AreEqual("www.bilibili.com",report.Sources.Single().Name);
    }
    [TestMethod] public void TemporaryAndFallbackSupportNeverBecomeDistraction()
    {
        var report=SessionReportBuilder.Build(Session,[Segment(600,StudyVerdict.Supportive,DecisionSource.Override),Segment(600,StudyVerdict.Supportive,DecisionSource.Fallback)],EscalationEngineTests.Options,5);
        Assert.AreEqual(1200,report.Supportive.TotalSeconds);Assert.AreEqual(TimeSpan.Zero,report.Distracted);Assert.AreEqual(0,report.Blocks);
    }
    [TestMethod] public void LegacyUnclassifiedEventsHaveNoInventedFocusRate()
    {
        var segment=Segment(50,StudyVerdict.Unknown) with {Decision=null};var report=SessionReportBuilder.Build(Session with {Intervention=null,SupportiveMinutes=null},[segment],EscalationEngineTests.Options,5);
        Assert.IsNull(report.FocusRate);Assert.AreEqual(50,report.Unclassified.TotalSeconds);
    }
    [TestMethod] public void ReturningToAnApprovedWindowDoesNotResetTheRecordedEpisode()
    {
        var segments=new[]{Segment(50,StudyVerdict.Distracted),Segment(5,StudyVerdict.Supportive,DecisionSource.Override),Segment(50,StudyVerdict.Distracted)};
        var live=new EscalationEngine(_clock,EscalationEngineTests.Options,SessionStrictness.Standard,TimeSpan.FromMinutes(5));
        foreach(var segment in segments) live.Record(segment);
        var report=SessionReportBuilder.Build(Session,segments,EscalationEngineTests.Options,5);
        Assert.AreEqual(live.Snapshot.EpisodeId,report.DistractionEpisodes);
        Assert.AreEqual(live.Snapshot.BlockCount,report.Blocks);
    }

    [TestMethod] public void PersistedResetInsideASegmentIsAppliedExactlyOnce()
    {
        var segment=Segment(100,StudyVerdict.Distracted);
        var report=SessionReportBuilder.Build(Session,[segment],EscalationEngineTests.Options,5,[segment.StartedAt.AddSeconds(50)]);
        Assert.AreEqual(2,report.DistractionEpisodes);Assert.AreEqual(0,report.Blocks);
        Assert.AreEqual(100,report.Distracted.TotalSeconds);
    }

}
