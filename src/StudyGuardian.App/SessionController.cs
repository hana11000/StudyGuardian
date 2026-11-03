using System.Windows.Threading;
using Microsoft.Win32;
using StudyGuardian.Core;
using StudyGuardian.Data;
using StudyGuardian.Monitor;

namespace StudyGuardian.App;

public sealed class SessionController : IAsyncDisposable
{
    private readonly MonitoringOptions _options;
    private readonly SqliteActivityRepository _repository;
    private readonly ForegroundWindowMonitor _monitor;
    private readonly IdleTimeProvider _idleProvider = new();
    private readonly SystemClock _clock = new();
    private readonly StudySession _session;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;
    private readonly SystemPauseState _systemPause = new();
    private Task _writes = Task.CompletedTask;
    private Task? _stopTask;
    private string? _sessionId;
    private TimeSpan _lastCheckpoint;
    private TimeSpan _idle;
    private bool _disposed;

    public bool IsReady { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsFaulted => ErrorMessage is not null;
    public bool HasSession => _sessionId is not null;
    public string? ErrorMessage { get; private set; }
    public string? ErrorDetail { get; private set; }
    public string DatabasePath => _repository.DatabasePath;
    public DateTime? LastSavedAt { get; private set; }
    public StudySessionOptions? Options { get; private set; }
    public StudySessionSnapshot Snapshot => _session.Snapshot;
    public event EventHandler? StateChanged;
    public event EventHandler<ActivitySegment>? SegmentSaved;

    public SessionController(MonitoringOptions options, SessionUiOptions sessionOptions, string databasePath, Dispatcher dispatcher)
    {
        _options = options;
        _repository = new(databasePath);
        _monitor = new(options.MaximumTitleCharacters);
        _dispatcher = dispatcher;
        _session = new(_clock, new(TimeSpan.FromSeconds(options.AwayAfterSeconds), TimeSpan.FromSeconds(sessionOptions.DecisionDwellSeconds),
            TimeSpan.FromMilliseconds(sessionOptions.RegexTimeoutMilliseconds)), options.ProtectedProcesses, Environment.ProcessId);
        _timer = new(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(options.IdleSampleMilliseconds) };
        _timer.Tick += TimerTick;
        _monitor.Changed += ForegroundChanged;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _repository.InitializeAsync();
            foreach (var segment in (await _repository.ReadRecentAsync(_options.RecentEventLimit)).Reverse())
                SegmentSaved?.Invoke(this, segment);
            SystemEvents.SessionSwitch += SessionSwitched;
            SystemEvents.PowerModeChanged += PowerChanged;
            IsReady = true;
            Notify();
        }
        catch (Exception ex) { Fail("无法打开本地记录", ex); }
    }

    public async Task StartAsync(StudySessionOptions options)
    {
        if (!IsReady || IsBusy || IsFaulted || HasSession || _disposed) return;
        IsBusy = true;
        Notify();
        try
        {
            Options = options with { Rules = options.Rules.ToArray() };
            _sessionId = Guid.NewGuid().ToString("N");
            await _repository.StartSessionAsync(_sessionId, _clock.UtcNow, Options);
            _idle = _idleProvider.GetIdleTime();
            _monitor.Start();
            _session.Start(_sessionId, Options, _monitor.Current, _idle);
            Queue(_session.SetSystemPause(_systemPause.Reason, _monitor.Current, _idle));
            _lastCheckpoint = _clock.Elapsed;
            _stopTask = null;
            _timer.Start();
        }
        catch (Exception ex) { Fail("无法开始会话", ex); }
        finally { IsBusy = false; Notify(); }
    }

    public Task SetPausedAsync(bool paused)
    {
        if (IsBusy || IsFaulted || _disposed || !Snapshot.IsRunning) return Task.CompletedTask;
        try
        {
            if (!_systemPause.Reason.HasValue)
            {
                _idle = _idleProvider.GetIdleTime();
                _monitor.Refresh();
            }
            Queue(_session.SetManualPause(paused, _monitor.Current, _idle));
            ProgressChanged();
        }
        catch (Exception ex) { Fail("暂停状态切换失败", ex); }
        return Task.CompletedTask;
    }

    private void ForegroundChanged(object? sender, WindowSnapshot window)
    {
        if (!Snapshot.IsRunning || IsFaulted || IsBusy) return;
        try
        {
            if (!_systemPause.Reason.HasValue) _idle = _idleProvider.GetIdleTime();
            Queue(_session.Observe(window, _idle));
            ProgressChanged();
        }
        catch (Exception ex) { Fail("窗口采集已停止", ex); }
    }

    private void TimerTick(object? sender, EventArgs e)
    {
        if (!Snapshot.IsRunning || IsFaulted || IsBusy) return;
        try
        {
            if (!_systemPause.Reason.HasValue) _idle = _idleProvider.GetIdleTime();
            Queue(_session.Observe(_monitor.Current, _idle));
            if (_clock.Elapsed - _lastCheckpoint >= TimeSpan.FromSeconds(_options.CheckpointSeconds))
            {
                Queue(_session.Checkpoint());
                _lastCheckpoint = _clock.Elapsed;
            }
            ProgressChanged();
        }
        catch (Exception ex) { Fail("会话计时已停止", ex); }
    }

    private void ProgressChanged()
    {
        Notify();
        if (!Snapshot.IsRunning && HasSession && !IsBusy && !IsFaulted) _ = StopAsync();
    }

    public Task StopAsync()
    {
        if (_stopTask is { IsCompleted: false }) return _stopTask;
        if (!HasSession) return Task.CompletedTask;
        return _stopTask = StopCoreAsync();
    }

    private async Task StopCoreAsync()
    {
        IsBusy = true;
        _timer.Stop();
        Notify();
        try
        {
            if (Snapshot.IsRunning)
            {
                if (!_systemPause.Reason.HasValue)
                {
                    _idle = _idleProvider.GetIdleTime();
                    Queue(_session.Observe(_monitor.Current, _idle));
                }
                Queue(_session.Stop());
            }
            _monitor.Stop();
            var logicalEnd = _session.EndedAt;
            await _writes;
            if (_sessionId is { } id && !IsFaulted)
            {
                await _repository.EndSessionAsync(id, logicalEnd ?? throw new InvalidOperationException("会话缺少结束时间"));
                _sessionId = null;
            }
        }
        catch (Exception ex) { Fail("保存会话失败", ex); }
        finally { IsBusy = false; Notify(); }
    }

    private void Queue(IReadOnlyList<ActivitySegment> segments)
    {
        foreach (var segment in segments)
            if (!IsFaulted) _writes = SaveAfterAsync(_writes, segment);
    }

    private async Task SaveAfterAsync(Task previous, ActivitySegment segment)
    {
        await previous;
        if (IsFaulted) return;
        try
        {
            await _repository.SaveSegmentAsync(segment);
            LastSavedAt = DateTime.Now;
            SegmentSaved?.Invoke(this, segment);
            Notify();
        }
        catch (Exception ex) { Fail("写入失败，部分最新记录未保存", ex); }
    }

    private void SessionSwitched(object sender, SessionSwitchEventArgs e)
    {
        if (_disposed || _dispatcher.HasShutdownStarted) return;
        _dispatcher.Invoke(() =>
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock: _systemPause.IsLocked = true; break;
                case SessionSwitchReason.SessionUnlock: _systemPause.IsLocked = false; break;
                case SessionSwitchReason.ConsoleDisconnect:
                case SessionSwitchReason.RemoteDisconnect: _systemPause.IsDisconnected = true; break;
                case SessionSwitchReason.ConsoleConnect:
                case SessionSwitchReason.RemoteConnect: _systemPause.IsDisconnected = false; break;
                default: return;
            }
            ApplySystemPause();
        });
    }

    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (_disposed || _dispatcher.HasShutdownStarted) return;
        _dispatcher.Invoke(() =>
        {
            if (e.Mode == PowerModes.Suspend) _systemPause.IsSuspended = true;
            else if (e.Mode == PowerModes.Resume) _systemPause.IsSuspended = false;
            else return;
            ApplySystemPause();
        });
    }

    private void ApplySystemPause()
    {
        if (!Snapshot.IsRunning || IsFaulted) return;
        try
        {
            if (!_systemPause.Reason.HasValue)
            {
                _idle = _idleProvider.GetIdleTime();
                _monitor.Refresh();
            }
            Queue(_session.SetSystemPause(_systemPause.Reason, _monitor.Current, _idle));
            ProgressChanged();
        }
        catch (Exception ex) { Fail("系统状态切换失败", ex); }
    }

    private void Fail(string message, Exception ex)
    {
        if (IsFaulted) return;
        ErrorMessage = message + "。请重新打开程序。";
        ErrorDetail = ex.Message;
        _timer.Stop();
        _monitor.Stop();
        _session.Stop();
        Notify();
    }

    private void Notify() => StateChanged?.Invoke(this, EventArgs.Empty);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync();
        _disposed = true;
        SystemEvents.SessionSwitch -= SessionSwitched;
        SystemEvents.PowerModeChanged -= PowerChanged;
        _timer.Tick -= TimerTick;
        _monitor.Changed -= ForegroundChanged;
        _monitor.Dispose();
    }
}
