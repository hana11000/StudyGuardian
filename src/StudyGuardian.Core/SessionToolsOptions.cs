namespace StudyGuardian.Core;

public sealed class SessionToolsOptions
{
    public required int AppealMinutes { get; init; }
    public required int EmergencyUses { get; init; }
    public required int EmergencyMinutes { get; init; }
    public required int BreakMinutes { get; init; }
    public required int MaximumAppealCharacters { get; init; }
    public required int ReportSessionLimit { get; init; }
    public void Validate()
    {
        if(AppealMinutes<=0 || EmergencyUses<=0 || EmergencyMinutes<=0 || BreakMinutes<=0 || MaximumAppealCharacters<=0 || ReportSessionLimit<=0)
            throw new ArgumentException("会话工具配置无效");
    }
}
public sealed record SessionToolsSnapshot(int EmergencyUsesRemaining, TimeSpan EmergencyRemaining, TimeSpan BreakRemaining);
