namespace StudyGuardian.Core;

public sealed record UserPreferences(InterventionOptions Intervention,IReadOnlyList<StudyRule> GlobalRules,AiPrivacy Privacy)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Intervention);Intervention.Validate();
        if(GlobalRules is null || Privacy is null || GlobalRules.Any(r=>r is null || RuleEngine.Validate(r)!=null)) throw new ArgumentException("全局规则无效");
    }
}
