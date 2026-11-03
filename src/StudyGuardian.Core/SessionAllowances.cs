namespace StudyGuardian.Core;

public sealed class SessionAllowances
{
    private readonly IClock _clock;
    private readonly SessionToolsOptions _options;
    private readonly Dictionary<string,TimeSpan> _appeals=[];
    private TimeSpan _emergencyUntil, _breakUntil;
    private int _remaining;
    public SessionAllowances(IClock clock,SessionToolsOptions options)
    { options.Validate();_clock=clock;_options=options;Reset(); }
    public SessionToolsSnapshot Snapshot => new(_remaining,Remaining(_emergencyUntil),Remaining(_breakUntil));
    public void Reset() { _appeals.Clear();_remaining=_options.EmergencyUses;_emergencyUntil=_breakUntil=TimeSpan.Zero; }
    public void GrantAppeal(WindowSnapshot window)
    {
        if(!window.IsAccessible || string.IsNullOrWhiteSpace(window.ProcessName)) throw new ArgumentException("无法放行这个窗口");
        foreach(var expired in _appeals.Where(x=>x.Value<=_clock.Elapsed).Select(x=>x.Key).ToArray()) _appeals.Remove(expired);
        _appeals[Identity(window)]=_clock.Elapsed+TimeSpan.FromMinutes(_options.AppealMinutes);
    }
    public bool TryUseEmergency()
    {
        if(_remaining<=0 || Remaining(_emergencyUntil)>TimeSpan.Zero) return false;
        _remaining--;_emergencyUntil=_clock.Elapsed+TimeSpan.FromMinutes(_options.EmergencyMinutes);return true;
    }
    public void BeginBreak() { if(Remaining(_breakUntil)==TimeSpan.Zero) _breakUntil=_clock.Elapsed+TimeSpan.FromMinutes(_options.BreakMinutes); }
    public void EndBreak() => _breakUntil=TimeSpan.Zero;
    public RuleDecision? DecisionFor(WindowSnapshot window)
    {
        if(!window.IsAccessible || string.IsNullOrWhiteSpace(window.ProcessName)) return null;
        if(Remaining(_emergencyUntil)>TimeSpan.Zero) return new(StudyVerdict.Supportive,DecisionSource.Override,1,"应急通行");
        return _appeals.TryGetValue(Identity(window),out var expiry) && _clock.Elapsed<expiry
            ? new(StudyVerdict.Supportive,DecisionSource.Override,1,"申诉通过") : null;
    }
    private TimeSpan Remaining(TimeSpan end) => end>_clock.Elapsed ? end-_clock.Elapsed : TimeSpan.Zero;
    private static string Identity(WindowSnapshot window)
    {
        var process=window.ProcessName.Trim().ToUpperInvariant();
        var target=Uri.TryCreate(window.Url,UriKind.Absolute,out var url) && url.Scheme is "http" or "https" ? url.AbsoluteUri : window.Title.Trim();
        return System.Text.Json.JsonSerializer.Serialize(new[]{process,window.Url is null?"title":"url",target});
    }
}
