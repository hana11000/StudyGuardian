namespace StudyGuardian.Core;

public sealed record InterventionOptions
{
    public required double ReminderSeconds { get; init; }
    public required double WarningSeconds { get; init; }
    public required double BlockSeconds { get; init; }
    public required double GentleMultiplier { get; init; }
    public required double StrictMultiplier { get; init; }
    public required double FocusResetSeconds { get; init; }
    public required double RollingWindowSeconds { get; init; }
    public required double AccelerationAfterSeconds { get; init; }
    public required double BlockLockSeconds { get; init; }
    public required double FrameThicknessPixels { get; init; }
    public required double OverlayOpacity { get; init; }
    public required bool SoundEnabled { get; init; }

    public void Validate()
    {
        double[] positives = [ReminderSeconds, WarningSeconds, BlockSeconds, GentleMultiplier, StrictMultiplier,
            FocusResetSeconds, RollingWindowSeconds, AccelerationAfterSeconds, BlockLockSeconds, FrameThicknessPixels, OverlayOpacity];
        if (positives.Any(v => !double.IsFinite(v) || v <= 0) || ReminderSeconds >= WarningSeconds || WarningSeconds >= BlockSeconds || OverlayOpacity > 1)
            throw new ArgumentOutOfRangeException(nameof(InterventionOptions));
    }
}

public enum InterventionLevel { None = -1, Grace = 0, Reminder = 1, Warning = 2, Block = 3 }
public sealed record EscalationSnapshot(InterventionLevel Level, bool IsDistracting, bool SupportiveOvertime, WindowSnapshot? Window,
    TimeSpan EpisodeDuration, TimeSpan TotalDistraction, TimeSpan RollingDistraction, TimeSpan UntilBlock,
    TimeSpan LockRemaining, int EpisodeId, int BlockCount);
