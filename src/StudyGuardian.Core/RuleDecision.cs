namespace StudyGuardian.Core;

public enum StudyVerdict { OnTask, Supportive, Distracted, Unknown, Neutral, Uncertain }
public enum DecisionSource { Rule, Fallback, Cache, Llm }

public sealed record RuleDecision(StudyVerdict Verdict, DecisionSource Source, double Confidence = 1, string? Reason = null)
{
    public bool CanEnforce => Source != DecisionSource.Fallback && Verdict is StudyVerdict.Distracted or StudyVerdict.Supportive;
    public static RuleDecision Unknown { get; } = new(StudyVerdict.Unknown, DecisionSource.Fallback);
    public static RuleDecision Neutral { get; } = new(StudyVerdict.Neutral, DecisionSource.Rule);
}
