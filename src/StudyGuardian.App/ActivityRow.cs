using System.Windows.Media;
using StudyGuardian.Core;

namespace StudyGuardian.App;

public sealed class ActivityRow(ActivitySegment segment)
{
    public string Id => segment.Id;
    public string LocalStart => segment.StartedAt.ToLocalTime().ToString("MM-dd HH:mm:ss");
    public string Process => string.IsNullOrEmpty(segment.Window.ProcessName) ? "不可读取" : segment.Window.ProcessName;
    public string Title => string.IsNullOrEmpty(segment.Window.Title) ? "（无标题）" : segment.Window.Title;
    public string Duration => $"{segment.Duration.TotalSeconds:0.0} 秒";
    public string Status => segment.PauseReason != PauseReason.None ? DisplayPauseReason(segment.PauseReason) :
        segment.Decision is { } decision ? DisplayVerdict(decision.Verdict) : "记录";
    public Brush StatusBrush => BrushFor(segment.PauseReason != PauseReason.None ? StudyVerdict.Neutral : segment.Decision?.Verdict ?? StudyVerdict.Unknown);

    public static string DisplayPauseReason(PauseReason reason) => reason switch
    {
        PauseReason.None => "有效",
        PauseReason.SystemWindow => "暂停",
        PauseReason.Away => "离开",
        PauseReason.Locked => "锁屏",
        PauseReason.Suspended => "睡眠",
        PauseReason.Manual => "已暂停",
        PauseReason.Break => "休息中",
        _ => "不可读取"
    };

    public static string FormatTime(TimeSpan duration) => $"{(long)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    public static string Countdown(TimeSpan duration)
    {
        var seconds = Math.Max(0, (long)Math.Ceiling(duration.TotalSeconds));
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }
    public static string DisplayVerdict(StudyVerdict verdict) => verdict switch
    {
        StudyVerdict.OnTask => "专注中", StudyVerdict.Supportive => "辅助", StudyVerdict.Distracted => "分心",
        StudyVerdict.Neutral => "暂停", StudyVerdict.Uncertain => "待确认", _ => "待判定"
    };
    public static string DisplayState(StudySessionSnapshot state) => !state.IsRunning ? state.IsCompleted ? "已完成" : "已结束" :
        state.PauseReason != PauseReason.None ? DisplayPauseReason(state.PauseReason) : state.Decision.Source==DecisionSource.Override ? state.Decision.Reason ?? "临时放行" :
        state.Decision is {Source:DecisionSource.Fallback,Verdict:StudyVerdict.Supportive} ? "暂未判定" : DisplayVerdict(state.Decision.Verdict);
    public static Brush BrushFor(StudyVerdict verdict) => (Brush)System.Windows.Application.Current.FindResource(verdict switch
    {
        StudyVerdict.OnTask => "SuccessBrush", StudyVerdict.Distracted => "DangerBrush", StudyVerdict.Neutral => "MutedBrush", _ => "AmberBrush"
    });
    public static Brush BrushForState(StudySessionSnapshot state, EscalationSnapshot? escalation)
    {
        if (!state.IsRunning || state.PauseReason != PauseReason.None) return BrushFor(StudyVerdict.Neutral);
        if (state.Decision.CanEnforce && escalation is { IsDistracting: true } && state.Window == escalation.Window)
            return BrushFor(escalation.Level >= InterventionLevel.Warning ? StudyVerdict.Distracted : StudyVerdict.Supportive);
        if (state.Decision.Verdict == StudyVerdict.Distracted) return BrushFor(StudyVerdict.Supportive);
        return BrushFor(state.Decision.Verdict);
    }
}
