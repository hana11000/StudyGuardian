namespace StudyGuardian.Core;

public sealed record StoredStudySession(string Id,string Goal,DateTimeOffset StartedAt,DateTimeOffset? EndedAt,
    SessionStrictness Strictness,string Status,InterventionOptions? Intervention,int? SupportiveMinutes);
public sealed record ReportSlice(DateTimeOffset StartedAt,TimeSpan Duration,StudyVerdict Verdict,string Process);
public sealed record DistractionSource(string Name,TimeSpan Duration);
public sealed record SessionReport(StoredStudySession Session,TimeSpan Focused,TimeSpan Supportive,TimeSpan Distracted,
    TimeSpan Paused,TimeSpan Unclassified,int DistractionEpisodes,int Blocks,IReadOnlyList<ReportSlice> Timeline,IReadOnlyList<DistractionSource> Sources)
{
    public double? FocusRate => Focused+Supportive+Distracted==TimeSpan.Zero ? null : Focused.TotalSeconds/(Focused+Supportive+Distracted).TotalSeconds;
}
public static class SessionReportBuilder
{
    private sealed class ReplayClock : IClock
    {
        private readonly DateTimeOffset _origin;
        public ReplayClock(DateTimeOffset origin) { _origin=origin;UtcNow=origin; }
        public DateTimeOffset UtcNow {get;set;}
        public TimeSpan Elapsed => UtcNow-_origin;
    }
    public static SessionReport Build(StoredStudySession session,IEnumerable<ActivitySegment> segments,InterventionOptions defaults,int defaultSupportiveMinutes,IEnumerable<DateTimeOffset>? resetTimes=null)
    {
        var records=segments.Where(s=>s.SessionId==session.Id && s.Duration>TimeSpan.Zero).GroupBy(s=>s.Id)
            .Select(g=>g.MaxBy(s=>s.Duration)!).OrderBy(s=>s.StartedAt).ToArray();
        var clock=new ReplayClock(records.FirstOrDefault()?.StartedAt ?? session.StartedAt);
        var engine=new EscalationEngine(clock,session.Intervention ?? defaults,session.Strictness,TimeSpan.FromMinutes(session.SupportiveMinutes ?? defaultSupportiveMinutes));
        long focused=0,supportive=0,paused=0,unknown=0;
        List<ReportSlice> timeline=[];Dictionary<string,long> sources=new(StringComparer.OrdinalIgnoreCase);
        foreach(var (segment,reset) in Replay(records,resetTimes ?? []))
        {
            clock.UtcNow=segment.EndedAt;
            if(reset) engine.ResetCurrentEpisode();
            var previous=engine.Snapshot.TotalDistraction;
            var state=engine.Record(segment);
            var distracted=Math.Min(segment.Duration.Ticks,(state.TotalDistraction-previous).Ticks);
            var other=segment.Duration.Ticks-distracted;
            StudyVerdict color;
            if(segment.PauseReason!=PauseReason.None) { paused+=other;color=StudyVerdict.Neutral; }
            else if(segment.Decision?.Verdict==StudyVerdict.OnTask) { focused+=other;color=StudyVerdict.OnTask; }
            else if(segment.Decision?.Verdict==StudyVerdict.Supportive) { supportive+=other;color=StudyVerdict.Supportive; }
            else { unknown+=other;color=StudyVerdict.Unknown; }
            if(other>0) timeline.Add(new(segment.StartedAt,TimeSpan.FromTicks(other),color,segment.Window.ProcessName));
            if(distracted>0)
            {
                timeline.Add(new(segment.StartedAt.AddTicks(other),TimeSpan.FromTicks(distracted),StudyVerdict.Distracted,segment.Window.ProcessName));
                var source=Uri.TryCreate(segment.Window.Url,UriKind.Absolute,out var url) && url.Scheme is "http" or "https" ? url.IdnHost : segment.Window.ProcessName;
                sources[source]=sources.GetValueOrDefault(source)+distracted;
            }
        }
        return new(session,TimeSpan.FromTicks(focused),TimeSpan.FromTicks(supportive),engine.Snapshot.TotalDistraction,
            TimeSpan.FromTicks(paused),TimeSpan.FromTicks(unknown),engine.Snapshot.EpisodeId,engine.Snapshot.BlockCount,timeline,
            sources.OrderByDescending(s=>s.Value).Select(s=>new DistractionSource(s.Key,TimeSpan.FromTicks(s.Value))).ToArray());
    }
    private static IEnumerable<(ActivitySegment Segment,bool Reset)> Replay(ActivitySegment[] records,IEnumerable<DateTimeOffset> resets)
    {
        var boundaries=resets.Distinct().Order().ToArray();var index=0;
        foreach(var original in records)
        {
            var start=original.StartedAt;var reset=false;var part=0;
            while(index<boundaries.Length && boundaries[index]<=start) {index++;reset=true;}
            while(index<boundaries.Length && boundaries[index]<original.EndedAt)
            {
                var end=boundaries[index++];
                yield return (original with {Id=original.Id+"/"+part++,StartedAt=start,EndedAt=end,Duration=end-start},reset);
                start=end;reset=true;
            }
            yield return (original with {Id=original.Id+"/"+part,StartedAt=start,Duration=original.EndedAt-start},reset);
        }
    }

}
