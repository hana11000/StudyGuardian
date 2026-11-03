namespace StudyGuardian.Core;

public enum StudyVerdict { OnTask, Supportive, Distracted, Unknown, Neutral }
public enum DecisionSource { Rule, Fallback }

public sealed record RuleDecision(StudyVerdict Verdict, DecisionSource Source)
{
    public static RuleDecision Unknown { get; } = new(StudyVerdict.Unknown, DecisionSource.Fallback);
    public static RuleDecision Neutral { get; } = new(StudyVerdict.Neutral, DecisionSource.Rule);
}
