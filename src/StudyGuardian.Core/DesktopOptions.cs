namespace StudyGuardian.Core;

public sealed record DesktopOptions
{
    public required string ExitPhrase {get;init;}
    public required double ExitDelaySeconds {get;init;}
    public required int UiTickMilliseconds {get;init;}
    public required int WatchdogPollMilliseconds {get;init;}
    public required int RestartDelayMilliseconds {get;init;}
    public required double RapidFailureSeconds {get;init;}
    public required int MaximumRapidRestarts {get;init;}
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(ExitPhrase) || !double.IsFinite(ExitDelaySeconds) || ExitDelaySeconds<=0 || ExitDelaySeconds>=TimeSpan.MaxValue.TotalSeconds ||
            UiTickMilliseconds<=0 || WatchdogPollMilliseconds<=0 || RestartDelayMilliseconds<=0 || !double.IsFinite(RapidFailureSeconds) || RapidFailureSeconds<=0 ||
            RapidFailureSeconds>=TimeSpan.MaxValue.TotalSeconds || MaximumRapidRestarts<=0) throw new ArgumentException("桌面配置无效");
    }
}
public sealed record DesktopPreferences(bool StrictExit=false,bool Watchdog=false);
public sealed class ExitGate
{
    private readonly IClock _clock;private readonly DesktopOptions _options;private TimeSpan? _accepted;
    public ExitGate(IClock clock,DesktopOptions options) {options.Validate();_clock=clock;_options=options;}
    public bool CanExit => _accepted.HasValue && Remaining==TimeSpan.Zero;
    public TimeSpan Remaining => TimeSpan.FromTicks(Math.Max(0,TimeSpan.FromSeconds(_options.ExitDelaySeconds).Ticks-(_accepted.HasValue?(_clock.Elapsed-_accepted.Value).Ticks:0)));
    public void SetText(string text)
    {if(!string.Equals(text,_options.ExitPhrase,StringComparison.Ordinal)) _accepted=null;else _accepted??=_clock.Elapsed;}
}
public sealed class RestartBudget
{
    private readonly IClock _clock;private readonly DesktopOptions _options;private TimeSpan _started;private int _restarts;
    public RestartBudget(IClock clock,DesktopOptions options) {options.Validate();_clock=clock;_options=options;Started();}
    public void Started() => _started=_clock.Elapsed;
    public bool TryRestart()
    {
        if(_clock.Elapsed-_started>=TimeSpan.FromSeconds(_options.RapidFailureSeconds)) _restarts=0;
        if(_restarts>=_options.MaximumRapidRestarts) return false;
        _restarts++;return true;
    }
}
