namespace StudyGuardian.Core;

public sealed class SystemPauseState
{
    public bool IsLocked { get; set; }
    public bool IsDisconnected { get; set; }
    public bool IsSuspended { get; set; }
    public PauseReason? Reason => IsSuspended ? PauseReason.Suspended
        : IsLocked || IsDisconnected ? PauseReason.Locked : null;
}
