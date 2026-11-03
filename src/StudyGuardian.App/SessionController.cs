using System.Windows.Threading;
using System.Net.Http;
using System.IO;
using System.Text.Json;
using StudyGuardian.AI;
using Microsoft.Win32;
using StudyGuardian.Core;
using StudyGuardian.Data;
using StudyGuardian.Monitor;

namespace StudyGuardian.App;

public sealed class SessionController : IAsyncDisposable
{
    private readonly MonitoringOptions _options;
    private readonly SqliteActivityRepository _repository;
    private readonly IForegroundWindowMonitor _monitor;
    private readonly IIdleTimeProvider _idleProvider;
    private readonly IClock _clock;
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
    private readonly AiOptions _aiOptions;
    private readonly AiSettingsStore _aiSettings;
    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false });
    private readonly List<ActivitySegment> _recent = [];
    private DecisionPipeline? _pipeline;
    private CancellationTokenSource? _aiCancellation;
    private long? _requestedRevision;
    private Task _aiWork = Task.CompletedTask;
    private readonly IStudyAi? _decisionAi;
    private readonly InterventionOptions _interventionOptions;
    private readonly SessionToolsOptions _toolsOptions;
    private readonly UserSettingsStore _preferences;
    private readonly SessionAllowances _allowances;
    private bool _breaking;
    private CancellationTokenSource? _sessionLifetime;
    public SessionToolsSnapshot Tools => _allowances.Snapshot;
    public bool IsOnBreak => _breaking;
    public int AppealMinutes => _toolsOptions.AppealMinutes;
    public int MaximumAppealCharacters => _toolsOptions.MaximumAppealCharacters;
    public WindowSnapshot? AppealTarget {get;private set;}
    public string? LastCompletedSessionId {get;private set;}
    public bool CanUseTools => Snapshot.IsRunning && !IsBusy && !IsFaulted && !_breaking && !_systemPause.Reason.HasValue &&
        (_escalation?.Snapshot.LockRemaining ?? TimeSpan.Zero)==TimeSpan.Zero;
    public bool CanAppeal => CanUseTools && _aiSettings.IsConfigured && AppealTarget is not null;
    private EscalationEngine? _escalation;
    private readonly BrowserOptions _browserOptions;
    private readonly BrowserActivityTracker _browser;
    private LoopbackBrowserServer? _browserServer;
    private bool _browserReady;
    private TimeSpan? _browserContact;
    public BrowserPairingStore BrowserPairing { get; }
    public int BrowserPort => _browserOptions.Port;
    public string BrowserConnectionStatus => !_browserReady ? "扩展连接不可用" : _browserContact is { } contact && _clock.Elapsed-contact <= TimeSpan.FromSeconds(_browserOptions.HeartbeatSeconds+_browserOptions.FreshnessSeconds) ? "已连接" : "等待扩展连接";
    public event EventHandler? ReturnRequested;
    private WindowSnapshot CurrentWindow => _browser.Enrich(_monitor.Current);
    private bool BrowserMonitoring => Snapshot.IsRunning && !Snapshot.IsManuallyPaused && !IsBusy && !IsFaulted && !_disposed &&
        !_systemPause.Reason.HasValue && !_breaking && Snapshot.PauseReason != PauseReason.Away;
    private static readonly JsonSerializerOptions BrowserJson = new(JsonSerializerDefaults.Web);

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
    public string AiStatus
    {
        get
        {
            if (!_aiSettings.Current.Enabled) return "AI 已关闭";
            if (!_aiSettings.HasKey) return "AI 未设置";
            if (!Snapshot.IsRunning) return "AI 就绪";
            if (Snapshot.PauseReason != PauseReason.None) return "AI 暂停";
            if (Snapshot.Decision is { Source: DecisionSource.Fallback, Verdict: StudyVerdict.Supportive } fallback)
                return $"{fallback.Reason} · {Math.Ceiling(_pipeline?.RetryRemaining.TotalSeconds ?? 0):0} 秒后重试";
            if (Snapshot.Decision.Verdict == StudyVerdict.Unknown)
                return _session.DecisionDwellRemaining > TimeSpan.Zero
                    ? $"等待判断 · {Math.Ceiling(_session.DecisionDwellRemaining.TotalSeconds):0} 秒" : "AI 判断中";
            var state = Snapshot.Decision.Source switch
            {
                DecisionSource.Rule => "本地规则", DecisionSource.Cache => "判定缓存", DecisionSource.Override => "临时放行",
                _ => Snapshot.Decision.Verdict == StudyVerdict.Uncertain ? "AI 信息不足" : "AI 已判断"
            };
            return $"{state} · AI {_pipeline?.AttemptCount ?? 0} 次";
        }
    }
    public string? AiStatusDetail => Snapshot.Decision.Reason;
    public EscalationSnapshot? Escalation => _escalation?.Snapshot;
    public WindowSnapshot? LastStudyWindow { get; private set; }
    public event EventHandler? StateChanged;
    public event EventHandler<ActivitySegment>? SegmentSaved;

    public SessionController(MonitoringOptions options, SessionUiOptions sessionOptions, AiOptions aiOptions, AiSettingsStore aiSettings, InterventionOptions interventionOptions, BrowserOptions browserOptions, SessionToolsOptions toolsOptions,UserSettingsStore preferences,string databasePath, Dispatcher dispatcher,
        IForegroundWindowMonitor? foregroundMonitor=null,IIdleTimeProvider? idleProvider=null,IClock? clock=null,IStudyAi? decisionAi=null)
    {
        _options = options;
        _clock=clock ?? new SystemClock();_idleProvider=idleProvider ?? new IdleTimeProvider();
        _aiOptions = aiOptions;
        _aiSettings = aiSettings;
        _decisionAi = decisionAi;
        _interventionOptions = interventionOptions;
        _toolsOptions=toolsOptions;_preferences=preferences;_allowances=new(_clock,toolsOptions);
        _browserOptions=browserOptions; _browser=new(_clock,browserOptions);
        BrowserPairing=new(Path.GetDirectoryName(databasePath)!,browserOptions);
        _repository = new(databasePath);
        _monitor = foregroundMonitor ?? new ForegroundWindowMonitor(options.MaximumTitleCharacters);
        _dispatcher = dispatcher;
        _session = new(_clock, new(TimeSpan.FromSeconds(options.AwayAfterSeconds), TimeSpan.FromSeconds(sessionOptions.DecisionDwellSeconds),
            TimeSpan.FromMilliseconds(sessionOptions.RegexTimeoutMilliseconds)), options.ProtectedProcesses, Environment.ProcessId,_allowances.DecisionFor);
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
            try
            {
                _=BrowserPairing.ReadOrCreate();
                _browserServer=new(_browserOptions,BrowserPairing.ReadOrCreate,
                    (request,ct)=>_dispatcher.InvokeAsync(()=>HandleBrowserRequest(request),DispatcherPriority.Normal,ct).Task);
                _browserServer.Start(); _browserReady=true;
            }
            catch(Exception) { _browserReady=false; }
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
            var rules=_preferences.Current.GlobalRules.Concat(options.Rules).ToArray();
            Options = options with { Rules=rules,Intervention=_preferences.Current.Intervention,
                Policy=new(options.Policy?.GoalSummary ?? options.Goal,rules,options.Policy?.SupportiveTimeLimitMinutes ?? _aiOptions.DefaultSupportiveMinutes) };
            _allowances.Reset();_breaking=false;AppealTarget=null;
            _sessionLifetime?.Dispose();_sessionLifetime=new();
            _recent.Clear(); _browser.Clear();
            LastStudyWindow = null;
            _escalation = new(_clock, Options.Intervention!, options.Strictness,
                TimeSpan.FromMinutes(options.Policy?.SupportiveTimeLimitMinutes ?? _aiOptions.DefaultSupportiveMinutes));
            _pipeline = _aiSettings.IsConfigured ? new(_clock, _decisionAi ?? CreateAiClient(), _repository, _aiOptions, _aiSettings.Current.Model) : null;
            _sessionId = Guid.NewGuid().ToString("N");
            await _repository.StartSessionAsync(_sessionId, _clock.UtcNow, Options);
            _idle = _idleProvider.GetIdleTime();
            _monitor.Start();
            _session.Start(_sessionId, Options, CurrentWindow, _idle);
            Queue(_session.SetSystemPause(_systemPause.Reason, CurrentWindow, _idle));
            _lastCheckpoint = _clock.Elapsed;
            _stopTask = null;
            _timer.Start();
        }
        catch (Exception ex) { Fail("无法开始会话", ex); }
        finally { IsBusy = false; Notify(); }
    }

    public Task SetPausedAsync(bool paused)
    {
        if(_breaking) {EndBreak();return Task.CompletedTask;}
        if (IsBusy || IsFaulted || _disposed || !Snapshot.IsRunning) return Task.CompletedTask;
        try
        {
            if (!_systemPause.Reason.HasValue)
            {
                _idle = _idleProvider.GetIdleTime();
                _monitor.Refresh();
            }
            _browser.Clear();
            Queue(_session.SetManualPause(paused, CurrentWindow, _idle));
            ProgressChanged();
        }
        catch (Exception ex) { Fail("暂停状态切换失败", ex); }
        return Task.CompletedTask;
    }

    private void ForegroundChanged(object? sender, WindowSnapshot window)
    {
        if (!Snapshot.IsRunning || IsFaulted || IsBusy || _breaking) return;
        try
        {
            if (!_systemPause.Reason.HasValue) _idle = _idleProvider.GetIdleTime();
            Queue(_session.Observe(_browser.Enrich(window), _idle));
            ProgressChanged();
        }
        catch (Exception ex) { Fail("窗口采集已停止", ex); }
    }

    private void TimerTick(object? sender, EventArgs e)
    {
        if (!Snapshot.IsRunning || IsFaulted || IsBusy) return;
        try
        {
            if(_breaking)
            {
                if(Tools.BreakRemaining==TimeSpan.Zero) {EndBreak();return;}
                Queue(_session.Observe(Snapshot.Window ?? WindowSnapshot.Unavailable,_idle));
                if(_clock.Elapsed-_lastCheckpoint>=TimeSpan.FromSeconds(_options.CheckpointSeconds)) {Queue(_session.Checkpoint());_lastCheckpoint=_clock.Elapsed;}
                ProgressChanged();return;
            }
            if (!_systemPause.Reason.HasValue) _idle = _idleProvider.GetIdleTime();
            Queue(_session.Observe(CurrentWindow, _idle));
            if (_pipeline?.CanRetry == true)
                Queue(_session.RetryFallbackDecision(CurrentWindow, _idle));
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
        if (_escalation is { } engine && _session.Checkpoint().SingleOrDefault() is { } current)
        {
            var oldLevel = engine.Snapshot.Level;
            var next = engine.Record(current);
            if (next.Level != oldLevel) Queue([current]);
        }
        if (Snapshot is { IsRunning: true, PauseReason: PauseReason.None, Decision: { Verdict: StudyVerdict.OnTask, Source: not DecisionSource.Fallback }, Window: { } study })
            LastStudyWindow = study;
        if (Snapshot.Window is { } browserWindow && _escalation?.Snapshot is { } escalation && BrowserMonitoring)
            _browser.ObserveEscalation(browserWindow, escalation, Options?.Policy?.GoalSummary ?? Options?.Goal ?? "");
        if(Snapshot is {PauseReason:PauseReason.None,Window:{ } target,Decision:{CanEnforce:true}} && _escalation?.Snapshot is {IsDistracting:true}) AppealTarget=target;
        ScheduleDecision();
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
        _sessionLifetime?.Cancel();
        CancelDecision();
        _timer.Stop();
        Notify();
        try
        {
            if (Snapshot.IsRunning)
            {
                if (!_systemPause.Reason.HasValue && !_breaking)
                {
                    _idle = _idleProvider.GetIdleTime();
                    Queue(_session.Observe(CurrentWindow, _idle));
                }
                Queue(_session.Stop());
            }
            _monitor.Stop();
            _browser.Clear();_breaking=false;_allowances.EndBreak();
            var logicalEnd = _session.EndedAt;
            await _aiWork;
            await _writes;
            if (_sessionId is { } id && !IsFaulted)
            {
                await _repository.EndSessionAsync(id, logicalEnd ?? throw new InvalidOperationException("会话缺少结束时间"));
                LastCompletedSessionId=id;
                _sessionId = null;
            }
        }
        catch (Exception ex) { Fail("保存会话失败", ex); }
        finally { IsBusy = false; Notify(); }
    }

    private void Queue(IReadOnlyList<ActivitySegment> segments)
    {
        foreach (var segment in segments)
        {
            var escalation = _escalation?.Record(segment);
            var enriched = segment with { Level = escalation is { IsDistracting: true } ? Math.Max(0, (int)escalation.Level) : 0 };
            if (segment.PauseReason == PauseReason.None && segment.Decision?.Verdict is StudyVerdict.OnTask or StudyVerdict.Supportive or StudyVerdict.Distracted)
            {
                _recent.RemoveAll(s => s.Id == segment.Id);
                _recent.Add(segment);
                while (_recent.Count > _aiOptions.RecentActivityLimit) _recent.RemoveAt(0);
            }
            if (!IsFaulted) _writes = SaveAfterAsync(_writes, enriched);
        }
    }

    public void BeginBreak()
    {
        if(!CanUseTools) return;
        try
        {
            _idle=_idleProvider.GetIdleTime();_monitor.Refresh();
            Queue(_session.SetBreakPause(true,CurrentWindow,_idle));
            _allowances.BeginBreak();_breaking=true;_monitor.Stop();CancelDecision();_browser.Clear();
            ResetEscalation();ProgressChanged();
        }
        catch(Exception ex) {Fail("无法开始休息",ex);}
    }
    public void EndBreak()
    {
        if(!_breaking || IsBusy || _disposed) return;
        try
        {
            _allowances.EndBreak();_breaking=false;
            if(!_systemPause.Reason.HasValue) _idle=_idleProvider.GetIdleTime();
            _monitor.Start();Queue(_session.SetBreakPause(false,CurrentWindow,_idle));
            _lastCheckpoint=_clock.Elapsed;ProgressChanged();
        }
        catch(Exception ex) {Fail("无法恢复会话",ex);}
    }
    public bool UseEmergencyPass()
    {
        if(!CanUseTools || !_allowances.TryUseEmergency()) return false;
        ApplyAllowance();return true;
    }
    public async Task<AppealDecision> AppealAsync(WindowSnapshot target,string explanation,CancellationToken cancellationToken)
    {
        if(!CanAppeal || _sessionId is not { } sessionId || Options is null) throw new AiUnavailableException("当前无法申诉");
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,_sessionLifetime!.Token);
        var request=new DecisionRequest(Options.Goal,Options.Rules,target,[],0);
        var result=await CreateAiClient().ReviewAppealAsync(new(request,explanation),linked.Token);
        linked.Token.ThrowIfCancellationRequested();
        if(_sessionId!=sessionId || !Snapshot.IsRunning) return new(false,0,"会话已结束");
        if(!CanUseTools) return new(false,0,"当前会话已暂停");
        if(result.Approved) {_allowances.GrantAppeal(target);ApplyAllowance();}
        return result;
    }
    private void ApplyAllowance()
    {
        CancelDecision();_idle=_idleProvider.GetIdleTime();_monitor.Refresh();
        Queue(_session.Observe(CurrentWindow,_idle));_browser.Clear();ResetEscalation();ProgressChanged();
    }
    private void ResetEscalation()
    {
        _escalation?.ResetCurrentEpisode();
        if(_sessionId is { } id) _writes=SaveResetAfterAsync(_writes,id,_session.ObservedAt);
    }
    private async Task SaveResetAfterAsync(Task previous,string sessionId,DateTimeOffset at)
    {
        await previous;if(IsFaulted) return;
        try {await _repository.SaveEscalationResetAsync(sessionId,at);}
        catch(Exception ex) {Fail("无法保存会话边界",ex);}
    }
    public Task<IReadOnlyList<StoredStudySession>> ReadReportsAsync() => _repository.ReadSessionsAsync(_toolsOptions.ReportSessionLimit);
    public async Task<SessionReport> ReadReportAsync(StoredStudySession session) => SessionReportBuilder.Build(session,
        await _repository.ReadSessionSegmentsAsync(session.Id),_interventionOptions,_aiOptions.DefaultSupportiveMinutes,await _repository.ReadEscalationResetsAsync(session.Id));

    public void ResetBrowserPairing() { BrowserPairing.Reset(); _browser.Clear(); }

    private object HandleBrowserRequest(BrowserRequest request)
    {
        _browserContact=_clock.Elapsed;
        if(request.Path=="/status") return new BrowserStatus(BrowserMonitoring);
        if(request.Path=="/return") { if(!_disposed) ReturnRequested?.Invoke(this,EventArgs.Empty); return new BrowserCommand(); }
        if(!BrowserMonitoring) return new BrowserCommand();
        if(request.Path=="/command")
        {
            return request.Body.TryGetProperty("clientId",out var id) && id.ValueKind==JsonValueKind.String
                ? _browser.GetCommand(id.GetString() ?? "") : new BrowserCommand();
        }
        var report=request.Body.Deserialize<BrowserActivity>(BrowserJson);
        if(report==null) return new BrowserCommand();
        var foreground=_monitor.Refresh();
        _idle=_idleProvider.GetIdleTime();
        if(!_browser.Accept(report,foreground)) return new BrowserCommand();
        Queue(_session.Observe(CurrentWindow,_idle));
        ProgressChanged();
        var command=_browser.GetCommand(report.ClientId);
        return command.Action=="block" ? command : new BrowserCommand(_escalation?.Snapshot is {IsDistracting:true,Level:>=InterventionLevel.Reminder} ? "warn" : "none");
    }

    public Task<StudyPolicy> GeneratePolicyAsync(string goal, CancellationToken cancellationToken)
    {
        if (!_aiSettings.Current.Enabled) throw new AiUnavailableException("请在设置中启用 AI");
        return CreateAiClient().GeneratePolicyAsync(goal, cancellationToken);
    }

    private OpenAiStudyClient CreateAiClient() => new(_http, _aiSettings.ReadKey, _aiOptions, _aiSettings.Current.Model,_preferences.Current.Privacy,_toolsOptions);

    public IReadOnlyList<RuleCandidate> GetRuleCandidates() => RuleCandidate.FromWindows(
        new WindowCatalog(_options.MaximumTitleCharacters).ReadVisibleWindows().Select(_browser.Enrich),
        _options.ProtectedProcesses, _aiOptions.BrowserProcesses, Environment.ProcessId);

    public async Task TestAiConnectionAsync(string? enteredKey, string model, bool useSavedKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model)) throw new AiUnavailableException("请输入模型名称");
        var client = new OpenAiStudyClient(_http, () => !string.IsNullOrWhiteSpace(enteredKey) ? enteredKey.Trim() : useSavedKey ? _aiSettings.ReadKey() : null,
            _aiOptions, model.Trim(), new AiPrivacy(), _toolsOptions);
        await client.ClassifyAsync(new("学习税法", [], new(0,0,"notepad.exe","税法学习笔记"), [], 0), cancellationToken);
    }

    private void ScheduleDecision()
    {
        var ticket = _session.PendingDecision;
        if (ticket is null || _pipeline is null || IsBusy || IsFaulted || _disposed) { CancelDecision(); return; }
        if (_requestedRevision == ticket.Revision) return;
        CancelDecision();
        _requestedRevision = ticket.Revision;
        _aiCancellation = new();
        _aiWork = ResolveDecisionAsync(ticket, _pipeline, _aiCancellation);
    }

    private async Task ResolveDecisionAsync(DecisionTicket ticket, DecisionPipeline pipeline, CancellationTokenSource cancellation)
    {
        try
        {
            var request = new DecisionRequest(Options!.Goal, Options.Rules, ticket.Window,
                _recent.Select(s => new RecentActivity(s.Window.ProcessName, s.Window.Title, s.Decision!.Verdict, s.Duration.TotalSeconds)).ToArray(), ticket.DwellSeconds);
            RuleDecision decision;
            try { decision = await pipeline.DecideAsync(request, RuleDecision.Unknown, cancellation.Token); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }
            catch (Exception)
            {
                if (cancellation.IsCancellationRequested) return;
                decision = pipeline.DegradeAfterUnexpectedFailure();
            }
            if (cancellation.IsCancellationRequested || _disposed || IsBusy || !Snapshot.IsRunning) return;
            _idle = _idleProvider.GetIdleTime();
            Queue(_session.ApplyExternalDecision(ticket, decision, CurrentWindow, _idle));
            ProgressChanged();
        }
        finally
        {
            if (ReferenceEquals(_aiCancellation, cancellation)) _aiCancellation = null;
            cancellation.Dispose();
        }
    }

    private void CancelDecision()
    {
        _aiCancellation?.Cancel();
        _aiCancellation = null;
        _requestedRevision = null;
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
            if(_systemPause.Reason.HasValue) _browser.Clear();
            if (!_systemPause.Reason.HasValue && !_breaking)
            {
                _idle = _idleProvider.GetIdleTime();
                _monitor.Refresh();
            }
            Queue(_session.SetSystemPause(_systemPause.Reason, CurrentWindow, _idle));
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
        CancelDecision();
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
        _http.Dispose();_sessionLifetime?.Dispose();
        if(_browserServer!=null) await _browserServer.DisposeAsync();
    }
}
