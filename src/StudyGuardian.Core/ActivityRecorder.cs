namespace StudyGuardian.Core;

public sealed class ActivityRecorder
{
    private readonly IClock _clock;
    private readonly TimeSpan _awayThreshold;
    private readonly HashSet<string> _protectedProcesses;
    private readonly int _ownProcessId;
    private bool _running;
    private string _sessionId = "";
    private string _segmentId = "";
    private WindowSnapshot? _window;
    private PauseReason _reason;
    private PauseReason? _forcedReason;
    private RuleDecision? _decision;
    private DateTimeOffset _originUtc;
    private TimeSpan _originElapsed;
    private TimeSpan _segmentStart;
    private TimeSpan _lastObserved;
    private TimeSpan _closedActive;
    private TimeSpan _stoppedAt;

    public DateTimeOffset ObservedAt => _originUtc + (_lastObserved - _originElapsed);

    public DateTimeOffset? EndedAt => !_running && _window is not null
        ? _originUtc + (_stoppedAt - _originElapsed) : null;

    public ActivityRecorder(IClock clock, TimeSpan awayThreshold, IEnumerable<string> protectedProcesses, int ownProcessId)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (awayThreshold <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(awayThreshold));
        _clock = clock;
        _awayThreshold = awayThreshold;
        _protectedProcesses = new(protectedProcesses.Select(NormalizeProcess), StringComparer.OrdinalIgnoreCase);
        _ownProcessId = ownProcessId;
    }

    public RecordingSnapshot Snapshot
    {
        get
        {
            if (_window is null) return new(false, null, PauseReason.None, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);
            var now = _running ? Now : _stoppedAt;
            var duration = now - _segmentStart;
            var active = _closedActive + (_running && _reason == PauseReason.None ? duration : TimeSpan.Zero);
            return new(_running, _window, _reason, duration, active, now - _originElapsed, _decision);
        }
    }

    public void Start(string sessionId, WindowSnapshot window, TimeSpan idle, RuleDecision? decision = null)
    {
        if (_running) throw new InvalidOperationException("已有采集会话正在进行。");
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(window);
        _sessionId = sessionId;
        _originUtc = _clock.UtcNow.ToUniversalTime();
        _originElapsed = _clock.Elapsed;
        _segmentStart = _lastObserved = _originElapsed;
        _closedActive = TimeSpan.Zero;
        _window = window;
        _reason = Classify(window, idle);
        _decision = ForReason(_reason, decision);
        _forcedReason = null;
        _segmentId = Guid.NewGuid().ToString("N");
        _running = true;
    }

    public IReadOnlyList<ActivitySegment> Observe(WindowSnapshot window, TimeSpan idle, RuleDecision? decision = null)
    {
        if (!_running) return [];
        ArgumentNullException.ThrowIfNull(window);
        var now = Now;
        if (_forcedReason.HasValue)
        {
            _lastObserved = now;
            return [];
        }

        List<ActivitySegment> segments = [];
        // Split at the measured input boundary, even if the UI timer was late.
        if (BaseReason(_window!) == PauseReason.None)
        {
            if (_reason == PauseReason.None && idle >= _awayThreshold)
                Transition(Clamp(now - idle + _awayThreshold, _lastObserved, now), _window!, PauseReason.Away, segments, _decision);
            else if (_reason == PauseReason.Away && idle < _awayThreshold)
                Transition(Clamp(now - idle, _lastObserved, now), _window!, PauseReason.None, segments, decision);
        }

        var nextReason = Classify(window, idle);
        if (_window != window || _reason != nextReason || _decision != ForReason(nextReason, decision))
            Transition(now, window, nextReason, segments, decision);
        _lastObserved = now;
        return segments;
    }

    public IReadOnlyList<ActivitySegment> Suspend(PauseReason reason)
    {
        if (reason is not (PauseReason.Locked or PauseReason.Suspended or PauseReason.Manual or PauseReason.Break))
            throw new ArgumentOutOfRangeException(nameof(reason));
        if (!_running) return [];
        List<ActivitySegment> segments = [];
        var now = Now;
        if (_reason != reason) Transition(now, _window!, reason, segments, _decision);
        _forcedReason = reason;
        _lastObserved = now;
        return segments;
    }

    public IReadOnlyList<ActivitySegment> Resume(WindowSnapshot window, TimeSpan idle, RuleDecision? decision = null)
    {
        if (!_running) return [];
        if (!_forcedReason.HasValue) return Observe(window, idle, decision);
        List<ActivitySegment> segments = [];
        var now = Now;
        _forcedReason = null;
        Transition(now, window, Classify(window, idle), segments, decision);
        _lastObserved = now;
        return segments;
    }

    // Never persist beyond the last classified instant: the next input sample may
    // backdate an idle boundary, and a newer snapshot would overlap that split.
    public ActivitySegment? Checkpoint() => _running ? MakeSegment(_lastObserved) : null;

    public ActivitySegment? Stop()
    {
        if (!_running) return null;
        _stoppedAt = Now;
        var segment = MakeSegment(_stoppedAt);
        if (segment?.PauseReason == PauseReason.None) _closedActive += segment.Duration;
        _running = false;
        _forcedReason = null;
        return segment;
    }

    private TimeSpan Now => _clock.Elapsed < _lastObserved ? _lastObserved : _clock.Elapsed;

    private PauseReason Classify(WindowSnapshot window, TimeSpan idle)
    {
        var reason = BaseReason(window);
        return reason != PauseReason.None ? reason : idle >= _awayThreshold ? PauseReason.Away : PauseReason.None;
    }

    private PauseReason BaseReason(WindowSnapshot window)
    {
        if (!window.IsAccessible || window.Handle == 0 || string.IsNullOrWhiteSpace(window.ProcessName))
            return PauseReason.Unavailable;
        return window.ProcessId == _ownProcessId || _protectedProcesses.Contains(NormalizeProcess(window.ProcessName))
            ? PauseReason.SystemWindow : PauseReason.None;
    }

    private void Transition(TimeSpan at, WindowSnapshot window, PauseReason reason, List<ActivitySegment> output, RuleDecision? decision = null)
    {
        var closed = MakeSegment(at);
        if (closed is not null)
        {
            output.Add(closed);
            if (closed.PauseReason == PauseReason.None) _closedActive += closed.Duration;
        }
        _window = window;
        _reason = reason;
        _decision = ForReason(reason, decision);
        _segmentStart = at;
        _segmentId = Guid.NewGuid().ToString("N");
    }

    private ActivitySegment? MakeSegment(TimeSpan at) => at <= _segmentStart ? null :
        new(_segmentId, _sessionId, _window!, _originUtc + (_segmentStart - _originElapsed),
            _originUtc + (at - _originElapsed), at - _segmentStart, _reason, _decision);

    private static RuleDecision? ForReason(PauseReason reason, RuleDecision? decision) => decision is null ? null :
        reason == PauseReason.None ? decision : reason is PauseReason.SystemWindow or PauseReason.Unavailable ? RuleDecision.Neutral :
        new(StudyVerdict.Neutral, DecisionSource.Fallback);

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max) => value < min ? min : value > max ? max : value;
    private static string NormalizeProcess(string value) => value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;
}
