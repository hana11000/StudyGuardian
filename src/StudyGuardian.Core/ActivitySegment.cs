namespace StudyGuardian.Core;

public enum PauseReason { None, SystemWindow, Away, Unavailable, Locked, Suspended, Manual, Break }

public sealed record ActivitySegment(
    string Id,
    string SessionId,
    WindowSnapshot Window,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    TimeSpan Duration,
    PauseReason PauseReason,
    RuleDecision? Decision = null,
    int Level = 0)
{
    public double ActiveSeconds => PauseReason == PauseReason.None ? Duration.TotalSeconds : 0;
}

public sealed record RecordingSnapshot(
    bool IsRunning,
    WindowSnapshot? Window,
    PauseReason PauseReason,
    TimeSpan CurrentDuration,
    TimeSpan ActiveDuration,
    TimeSpan TotalDuration,
    RuleDecision? Decision = null);
