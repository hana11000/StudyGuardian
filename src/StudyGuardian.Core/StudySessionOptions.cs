namespace StudyGuardian.Core;

public enum SessionStrictness { Gentle, Standard, Strict }
public sealed record StudySessionOptions(string Goal, TimeSpan Duration, SessionStrictness Strictness, IReadOnlyList<StudyRule> Rules, StudyPolicy? Policy = null, InterventionOptions? Intervention = null);
public sealed record StudyTimingOptions(TimeSpan AwayThreshold, TimeSpan DecisionDwell, TimeSpan RegexTimeout);
