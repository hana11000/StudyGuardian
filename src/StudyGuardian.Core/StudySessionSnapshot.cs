namespace StudyGuardian.Core;

public sealed record StudySessionSnapshot(bool IsRunning, bool IsManuallyPaused, bool IsCompleted,
    WindowSnapshot? Window, PauseReason PauseReason, RuleDecision Decision, TimeSpan Remaining, double? FocusRate);
