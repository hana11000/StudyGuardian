namespace StudyGuardian.Core;

public sealed class StudySession
{
    private readonly IClock _clock;
    private readonly StudyTimingOptions _timing;
    private readonly string[] _protectedProcesses;
    private readonly int _ownProcessId;
    private readonly ActivityRecorder _recorder;
    private readonly Func<WindowSnapshot,RuleDecision?>? _temporaryDecision;
    private RuleEngine? _engine;
    private StudySessionOptions? _options;
    private WindowSnapshot? _dwellWindow;
    private TimeSpan _dwellStarted;
    private RuleDecision? _dwellDecision;
    private bool _wasPaused;
    private bool _manualPaused;
    private bool _breakPaused;
    private PauseReason? _systemPause;
    private bool _completed;
    private long _focusedTicks;
    private long _classifiedTicks;
    private long _closedActiveTicks;
    private DateTimeOffset? _budgetEndedAt;
    private long _visitRevision;

    public StudySession(IClock clock, StudyTimingOptions timing, IEnumerable<string> protectedProcesses, int ownProcessId, Func<WindowSnapshot,RuleDecision?>? temporaryDecision = null)
    {
        if (timing.DecisionDwell < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timing));
        _clock = clock;
        _temporaryDecision=temporaryDecision;
        _timing = timing;
        _protectedProcesses = protectedProcesses.ToArray();
        _ownProcessId = ownProcessId;
        _recorder = new(clock, timing.AwayThreshold, _protectedProcesses, ownProcessId);
    }

    public DateTimeOffset ObservedAt => _recorder.ObservedAt;

    public void Start(string sessionId, StudySessionOptions options, WindowSnapshot window, TimeSpan idle)
    {
        if (_recorder.Snapshot.IsRunning) throw new InvalidOperationException("已有学习会话正在进行");
        if (string.IsNullOrWhiteSpace(options.Goal)) throw new ArgumentException("请输入学习目标", nameof(options));
        if (options.Duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options));
        if (!Enum.IsDefined(options.Strictness)) throw new ArgumentException("严格度无效", nameof(options));
        var engine = new RuleEngine(options.Rules, _protectedProcesses, _ownProcessId, _timing.RegexTimeout);
        _recorder.Start(sessionId, window, idle, RuleDecision.Unknown);
        _engine = engine;
        _options = options with { Goal = options.Goal.Trim(), Rules = options.Rules.ToArray() };
        _focusedTicks = _classifiedTicks = _closedActiveTicks = 0;
        _budgetEndedAt = null;
        _manualPaused = _breakPaused = _completed = false;
        _systemPause = null;
        ResetDwell(window);
        _wasPaused = _recorder.Snapshot.PauseReason != PauseReason.None;
    }

    public IReadOnlyList<ActivitySegment> Observe(WindowSnapshot window, TimeSpan idle)
    {
        if (!_recorder.Snapshot.IsRunning) return [];
        List<ActivitySegment> output = [];
        var changed = window != _dwellWindow;
        Collect(_recorder.Observe(window, idle, changed || _wasPaused ? RuleDecision.Unknown : _dwellDecision ?? RuleDecision.Unknown), output);
        var paused = _recorder.Snapshot.PauseReason != PauseReason.None;
        if (changed || paused || _wasPaused) ResetDwell(window);
        if(!paused && _temporaryDecision?.Invoke(window) is { Source:DecisionSource.Override,Verdict:StudyVerdict.Supportive } temporary)
        {
            if(_dwellDecision!=temporary) { _visitRevision++;_dwellDecision=temporary; }
            Collect(_recorder.Observe(window,idle,temporary),output);
        }
        else if(!paused)
        {
            if(_dwellDecision?.Source==DecisionSource.Override)
            {
                ResetDwell(window);
                Collect(_recorder.Observe(window,idle,RuleDecision.Unknown),output);
            }
            if(_clock.Elapsed-_dwellStarted>=_timing.DecisionDwell)
            {
                _dwellDecision ??= _engine!.Evaluate(window);
                Collect(_recorder.Observe(window,idle,_dwellDecision),output);
            }
        }
        _wasPaused = paused;
        if (_recorder.Snapshot.ActiveDuration >= _options!.Duration)
        {
            _completed = true;
            if (_recorder.Stop() is { } last) Collect([last], output);
        }
        return output;
    }

    public IReadOnlyList<ActivitySegment> SetManualPause(bool paused, WindowSnapshot window, TimeSpan idle)
    {
        List<ActivitySegment> output = [.. Observe(window, idle)];
        if (!_recorder.Snapshot.IsRunning || _manualPaused == paused) return output;
        _manualPaused = paused;
        ApplyPause(window, idle, output);
        return output;
    }

    public IReadOnlyList<ActivitySegment> SetBreakPause(bool paused,WindowSnapshot window,TimeSpan idle)
    {
        List<ActivitySegment> output=[..Observe(window,idle)];
        if(!_recorder.Snapshot.IsRunning || _breakPaused==paused) return output;
        _breakPaused=paused;ApplyPause(window,idle,output);return output;
    }

    public IReadOnlyList<ActivitySegment> SetSystemPause(PauseReason? reason, WindowSnapshot window, TimeSpan idle)
    {
        if (reason is not (null or PauseReason.Locked or PauseReason.Suspended)) throw new ArgumentOutOfRangeException(nameof(reason));
        List<ActivitySegment> output = [.. Observe(window, idle)];
        if (!_recorder.Snapshot.IsRunning) return output;
        if (_systemPause == reason) return output;
        _systemPause = reason;
        ApplyPause(window, idle, output);
        return output;
    }

    private void ApplyPause(WindowSnapshot window, TimeSpan idle, List<ActivitySegment> output)
    {
        var reason = _systemPause ?? (_breakPaused ? PauseReason.Break : _manualPaused ? PauseReason.Manual : (PauseReason?)null);
        Collect(reason.HasValue ? _recorder.Suspend(reason.Value) : _recorder.Resume(window, idle, RuleDecision.Unknown), output);
        ResetDwell(window);
        _wasPaused = _recorder.Snapshot.PauseReason != PauseReason.None;
    }

    public IReadOnlyList<ActivitySegment> Checkpoint() => _recorder.Checkpoint() is { } current ? [current] : [];

    public IReadOnlyList<ActivitySegment> Stop()
    {
        List<ActivitySegment> output = [];
        if (_recorder.Stop() is { } last) Collect([last], output);
        return output;
    }

    public DateTimeOffset? EndedAt => _budgetEndedAt ?? _recorder.EndedAt;

    public DecisionTicket? PendingDecision => Snapshot.IsRunning && Snapshot.PauseReason == PauseReason.None &&
        _dwellDecision?.Verdict == StudyVerdict.Unknown && _dwellWindow is { } window
            ? new(_visitRevision, window, (_clock.Elapsed - _dwellStarted).TotalSeconds) : null;

    public IReadOnlyList<ActivitySegment> ApplyExternalDecision(DecisionTicket ticket, RuleDecision decision, WindowSnapshot window, TimeSpan idle)
    {
        List<ActivitySegment> output = [.. Observe(window, idle)];
        if (PendingDecision is not { } pending || pending.Revision != ticket.Revision || pending.Window != ticket.Window) return output;
        if (decision.Source is not (DecisionSource.Llm or DecisionSource.Cache or DecisionSource.Fallback) ||
            decision.Verdict is StudyVerdict.Neutral or StudyVerdict.Unknown) return output;
        _dwellDecision = decision;
        Collect(_recorder.Observe(window, idle, decision), output);
        return output;
    }

    public StudySessionSnapshot Snapshot
    {
        get
        {
            var recording = _recorder.Snapshot;
            var focused = _focusedTicks;
            var classified = _classifiedTicks;
            if (recording.IsRunning && recording.PauseReason == PauseReason.None)
                AddDuration(recording.Decision, recording.CurrentDuration.Ticks, ref focused, ref classified);
            return new(recording.IsRunning, _manualPaused, _completed, recording.Window, recording.PauseReason,
                recording.Decision ?? RuleDecision.Unknown,
                TimeSpan.FromTicks(Math.Max(0, (_options?.Duration.Ticks ?? 0) - recording.ActiveDuration.Ticks)),
                classified == 0 ? null : (double)focused / classified);
        }
    }

    private void ResetDwell(WindowSnapshot window)
    {
        _visitRevision++;
        _dwellWindow = window;
        _dwellStarted = _clock.Elapsed;
        _dwellDecision = null;
    }

    private void Collect(IReadOnlyList<ActivitySegment> segments, List<ActivitySegment> output)
    {
        foreach (var original in segments)
        {
            if (_budgetEndedAt.HasValue) break;
            var segment = original;
            if (segment.PauseReason == PauseReason.None)
            {
                var remaining = Math.Max(0, _options!.Duration.Ticks - _closedActiveTicks);
                if (segment.Duration.Ticks >= remaining)
                {
                    var duration = TimeSpan.FromTicks(remaining);
                    segment = segment with { Duration = duration, EndedAt = segment.StartedAt + duration };
                    _budgetEndedAt = segment.EndedAt;
                    _completed = true;
                }
                _closedActiveTicks += segment.Duration.Ticks;
            }
            if (segment.Duration == TimeSpan.Zero) continue;
            output.Add(segment);
            if (segment.PauseReason == PauseReason.None)
                AddDuration(segment.Decision, segment.Duration.Ticks, ref _focusedTicks, ref _classifiedTicks);
        }
    }

    private static void AddDuration(RuleDecision? decision, long ticks, ref long focused, ref long classified)
    {
        if (decision?.Verdict is StudyVerdict.OnTask or StudyVerdict.Supportive or StudyVerdict.Distracted) classified += ticks;
        if (decision?.Verdict == StudyVerdict.OnTask) focused += ticks;
    }
}
