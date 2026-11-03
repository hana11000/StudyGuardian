namespace StudyGuardian.Core;

public sealed record StudyPolicy(string GoalSummary, IReadOnlyList<StudyRule> Rules, int SupportiveTimeLimitMinutes);
public sealed record RecentActivity(string Process, string Title, StudyVerdict Verdict, double Seconds);
public sealed record DecisionRequest(string Goal, IReadOnlyList<StudyRule> Rules, WindowSnapshot Current,
    IReadOnlyList<RecentActivity> Recent, double DwellSeconds);
public enum CacheScope { ExactUrl, Domain, Process, Title }
public sealed record AiDecision(StudyVerdict Verdict, double Confidence, string Reason, CacheScope CacheScope, int CacheTtlMinutes);
public sealed record DecisionTicket(long Revision, WindowSnapshot Window, double DwellSeconds);
public sealed record CachedDecision(string Key, RuleDecision Decision, DateTimeOffset ExpiresAt);
public sealed record AiPrivacy(bool SendWindowTitle=true, bool SendUrl=true);
public sealed record AppealRequest(DecisionRequest Context,string Explanation);
public sealed record AppealDecision(bool Approved,double Confidence,string Reason);
public interface IAppealReviewer
{
    Task<AppealDecision> ReviewAppealAsync(AppealRequest request,CancellationToken cancellationToken);
}

public interface IStudyAi
{
    Task<StudyPolicy> GeneratePolicyAsync(string goal, CancellationToken cancellationToken);
    Task<AiDecision> ClassifyAsync(DecisionRequest request, CancellationToken cancellationToken);
}

public interface IDecisionCache
{
    Task<CachedDecision?> FindDecisionAsync(string key, DateTimeOffset now, CancellationToken cancellationToken);
    Task SaveDecisionAsync(CachedDecision entry, CancellationToken cancellationToken);
}

public sealed class AiUnavailableException(string message) : Exception(message);

public sealed class AiOptions
{
    public required string Model { get; init; }
    public required double RequestTimeoutSeconds { get; init; }
    public required int CacheMaxTtlMinutes { get; init; }
    public required int FailureBackoffSeconds { get; init; }
    public required double MinimumConfidence { get; init; }
    public required int MaximumResponseBytes { get; init; }
    public required int MaximumOutputTokens { get; init; }
    public required int MaximumRules { get; init; }
    public required int MaximumGoalCharacters { get; init; }
    public required int MaximumPatternCharacters { get; init; }
    public required int MaximumReasonCharacters { get; init; }
    public string? ReasoningEffort { get; init; }
    public required int MaximumSupportiveMinutes { get; init; }
    public required int DefaultSupportiveMinutes { get; init; }
    public required int RecentActivityLimit { get; init; }
    public required string[] BrowserProcesses { get; init; }
    public required string[] MixedContentDomains { get; init; }
}
