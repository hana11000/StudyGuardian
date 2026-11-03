namespace StudyGuardian.Core;

public sealed class EscalationEngine
{
    private readonly IClock _clock;
    private readonly InterventionOptions _options;
    private readonly long _supportiveLimitTicks;
    private readonly long _reminderTicks, _warningTicks, _blockTicks;
    private readonly Dictionary<string, long> _seen = [];
    private readonly List<(DateTimeOffset Start, DateTimeOffset End)> _distractions = [];
    private DateTimeOffset? _cursor;
    private WindowSnapshot? _window, _supportiveWindow;
    private long _supportiveTicks, _focusTicks, _episodeTicks, _totalTicks, _offsetTicks;
    private bool _episodeActive, _isDistracting, _supportiveOvertime;
    private InterventionLevel _level = InterventionLevel.None;
    private TimeSpan _lockedUntil;
    private int _episodeId, _blockCount;

    public EscalationEngine(IClock clock, InterventionOptions options, SessionStrictness strictness, TimeSpan supportiveLimit)
    {
        options.Validate();
        if (supportiveLimit <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(supportiveLimit));
        _clock = clock; _options = options; _supportiveLimitTicks = supportiveLimit.Ticks;
        var multiplier = strictness switch { SessionStrictness.Gentle => options.GentleMultiplier, SessionStrictness.Strict => options.StrictMultiplier, _ => 1 };
        _reminderTicks = TimeSpan.FromSeconds(options.ReminderSeconds * multiplier).Ticks;
        _warningTicks = TimeSpan.FromSeconds(options.WarningSeconds * multiplier).Ticks;
        _blockTicks = TimeSpan.FromSeconds(options.BlockSeconds * multiplier).Ticks;
    }

    public EscalationSnapshot Snapshot => new(_level, _isDistracting, _supportiveOvertime, _window,
        TimeSpan.FromTicks(_episodeTicks), TimeSpan.FromTicks(_totalTicks), TimeSpan.FromTicks(_cursor is { } now ? RollingAt(now) : 0),
        TimeSpan.FromTicks(Math.Max(0, _blockTicks - _offsetTicks - _episodeTicks)),
        _level == InterventionLevel.Block ? TimeSpan.FromTicks(Math.Max(0, (_lockedUntil - _clock.Elapsed).Ticks)) : TimeSpan.Zero, _episodeId, _blockCount);

    public EscalationSnapshot Record(ActivitySegment segment)
    {
        if (segment.Duration < TimeSpan.Zero || segment.EndedAt < segment.StartedAt) throw new ArgumentException("片段时间无效", nameof(segment));
        _seen.TryGetValue(segment.Id, out var knownTicks);
        if (segment.Duration.Ticks <= knownTicks) return Snapshot;
        _seen[segment.Id] = segment.Duration.Ticks;
        var start = segment.StartedAt.AddTicks(knownTicks);
        if (_cursor is { } cursor && start < cursor) start = cursor;
        var end = segment.EndedAt;
        if (end <= start) return Snapshot;
        var ticks = (end - start).Ticks;
        _window = segment.Window; _isDistracting = _supportiveOvertime = false;
        var decision = segment.Decision;
        var active = segment.PauseReason == PauseReason.None;
        if (active && decision is { Verdict: StudyVerdict.OnTask, Source: not DecisionSource.Fallback })
        {
            _focusTicks += ticks; ResetSupportive();
            if (_focusTicks >= TimeSpan.FromSeconds(_options.FocusResetSeconds).Ticks) ResetEpisode();
        }
        else
        {
            _focusTicks = 0;
            if (active && decision is { Verdict: StudyVerdict.Distracted, CanEnforce: true })
            { ResetSupportive(); AddDistraction(start, end, supportive: false); }
            else if (active && decision is { Verdict: StudyVerdict.Supportive, CanEnforce: true })
            {
                if (_supportiveWindow != segment.Window) { _supportiveWindow = segment.Window; _supportiveTicks = 0; }
                var remaining = Math.Max(0, _supportiveLimitTicks - _supportiveTicks);
                _supportiveTicks += ticks;
                if (_supportiveTicks >= _supportiveLimitTicks)
                {
                    _supportiveOvertime = true;
                    AddDistraction(start.AddTicks(Math.Min(remaining, ticks)), end, supportive: true);
                }
            }
        }
        _cursor = end;
        var oldest = end.AddSeconds(-_options.RollingWindowSeconds);
        _distractions.RemoveAll(i => i.End <= oldest);
        return Snapshot;
    }

    private void AddDistraction(DateTimeOffset start, DateTimeOffset end, bool supportive)
    {
        _isDistracting = true;
        if (!_episodeActive)
        {
            _episodeActive = true; _episodeId++; _episodeTicks = 0;
            _offsetTicks = RollingAt(start) > TimeSpan.FromSeconds(_options.AccelerationAfterSeconds).Ticks ? _warningTicks : 0;
        }
        if (supportive) _offsetTicks = Math.Max(_offsetTicks, _reminderTicks - _episodeTicks);
        var ticks = (end - start).Ticks;
        _episodeTicks += ticks; _totalTicks += ticks;
        if (ticks > 0)
        {
            if (_distractions.Count > 0 && _distractions[^1].End == start) _distractions[^1] = (_distractions[^1].Start, end);
            else _distractions.Add((start, end));
        }
        var elapsed = _episodeTicks + _offsetTicks;
        var level = elapsed >= _blockTicks ? InterventionLevel.Block : elapsed >= _warningTicks ? InterventionLevel.Warning :
            elapsed >= _reminderTicks ? InterventionLevel.Reminder : InterventionLevel.Grace;
        if (level == InterventionLevel.Block && _level != InterventionLevel.Block)
        { _lockedUntil = _clock.Elapsed + TimeSpan.FromSeconds(_options.BlockLockSeconds); _blockCount++; }
        if (level > _level) _level = level;
    }

    private long RollingAt(DateTimeOffset now)
    {
        var cutoff = now.AddSeconds(-_options.RollingWindowSeconds);
        return _distractions.Sum(i => Math.Max(0, ((i.End < now ? i.End : now) - (i.Start > cutoff ? i.Start : cutoff)).Ticks));
    }
    private void ResetSupportive() { _supportiveWindow = null; _supportiveTicks = 0; }
    public void ResetCurrentEpisode()
    { ResetEpisode();ResetSupportive();_focusTicks=0;_isDistracting=_supportiveOvertime=false; }
    private void ResetEpisode()
    { _episodeActive = false; _episodeTicks = _offsetTicks = 0; _level = InterventionLevel.None; _lockedUntil = TimeSpan.Zero; }
}
