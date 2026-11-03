namespace StudyGuardian.Core;

public enum RuleKind { Allow, Supportive, Block }
public enum RuleMatchType { Process, Domain, Url, TitleRegex }
public sealed record StudyRule(string Id, RuleKind Kind, RuleMatchType MatchType, string Pattern);
