using System.Media;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using StudyGuardian.Core;
using StudyGuardian.Monitor;
using StudyGuardian.App.Controls;

namespace StudyGuardian.App;

internal sealed class InterventionPresenter : IDisposable
{
    private readonly InterventionOptions _options;
    private readonly NativeWindowActions _actions;
    private readonly Dispatcher _dispatcher;
    private readonly Action _showMain;
    private readonly Func<Rect?> _noticeAnchor;
    private readonly List<InterventionWindow> _surfaces = [];
    private InterventionLevel _displayed = InterventionLevel.None;
    private WindowSnapshot? _study;
    private WindowSnapshot? _interventionTarget;
    private (int Episode, long Handle)? _lastMinimized;
    private EscalationSnapshot? _state;
    private string _goal = "";
    private int _soundedEpisode;
    private bool _updating, _failed, _disposed;
    public event EventHandler<string>? Failed;
    public event EventHandler<WindowSnapshot>? AppealRequested;
    public event EventHandler? EmergencyRequested;
    private SessionToolsSnapshot _tools=new(0,TimeSpan.Zero,TimeSpan.Zero);
    private bool _canAppeal;

    public InterventionPresenter(InterventionOptions options, NativeWindowActions actions, Dispatcher dispatcher, Action showMain, Func<Rect?>? noticeAnchor=null)
    {
        _options = options; _actions = actions; _dispatcher = dispatcher; _showMain = showMain; _noticeAnchor=noticeAnchor ?? (()=>null);
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
    }

    public void Update(StudySessionSnapshot current, EscalationSnapshot? state, string goal, WindowSnapshot? study,SessionToolsSnapshot tools,bool canAppeal)
    {
        if (_updating || _disposed) return;
        _updating = true;
        try
        {
            if (!current.IsRunning) { HideAll(); _failed = false; _lastMinimized = null; _interventionTarget = null; _soundedEpisode = 0; return; }
            if (_failed) return;
            _study = study; _state = state; _goal = goal;_tools=tools;_canAppeal=canAppeal;
            var ownOverlay = current.Window is { } window && _surfaces.Any(s => s.Handle == window.Handle) && _displayed == InterventionLevel.Block;
            var eligible = current.PauseReason == PauseReason.None && state is { IsDistracting: true } && current.Window == state.Window && current.Decision.CanEnforce;
            if (current.IsManuallyPaused || current.PauseReason is PauseReason.Locked or PauseReason.Suspended or PauseReason.Away or PauseReason.Break || state is null ||
                state.Level < InterventionLevel.Reminder || (!eligible && !ownOverlay)) { HideAll(); return; }
            if (!ownOverlay && current.Window is { } target && !_actions.CanIntervene(target)) { HideAll(); return; }
            if (eligible) _interventionTarget = current.Window;
            if (state.Level >= InterventionLevel.Warning && _soundedEpisode != state.EpisodeId)
            { if (_options.SoundEnabled) SystemSounds.Exclamation.Play(); _soundedEpisode = state.EpisodeId; }
            if (state.Level == InterventionLevel.Block && !ownOverlay && current.Window is { } distracting && _lastMinimized != (state.EpisodeId, distracting.Handle))
            { _actions.TryMinimize(distracting); _lastMinimized = (state.EpisodeId, distracting.Handle); }
            if (_displayed != state.Level) Rebuild(state.Level);
            foreach (var surface in _surfaces) surface.Update(goal, state, ReturnProcess,_tools,_canAppeal);
            RefreshPlacement();
        }
        catch (Exception) { Fail(); }
        finally { _updating = false; }
    }

    private void Rebuild(InterventionLevel level)
    {
        HideAll();
        var monitors = DisplayMonitorProvider.GetMonitors();
        if (monitors.Count == 0) throw new InvalidOperationException("没有可用显示器");
        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        if (level is InterventionLevel.Warning or InterventionLevel.Block)
            foreach (var monitor in monitors)
                AddSurface(level == InterventionLevel.Block ? InterventionSurface.Block : InterventionSurface.Frame, monitor, level, level == InterventionLevel.Block && monitor == primary);
        if (level != InterventionLevel.Block) AddSurface(InterventionSurface.Notice, WindowPlacement.SelectMonitor(monitors,_noticeAnchor()), level, false);
        _displayed = level;
    }

    private void AddSurface(InterventionSurface kind, DisplayMonitor monitor, InterventionLevel level, bool activate)
    {
        var surface = new InterventionWindow(kind, _options, activate);
        surface.ReturnRequested += (_, _) => ReturnToStudy();
        surface.AppealRequested+=(_,_)=> {if(_interventionTarget is { } target) {HideAll();_showMain();AppealRequested?.Invoke(this,target);}};
        surface.EmergencyRequested+=(_,_)=> {HideAll();_showMain();EmergencyRequested?.Invoke(this,EventArgs.Empty);};
        _surfaces.Add(surface);
        surface.Update(_goal, _state!, ReturnProcess,_tools,_canAppeal);
        surface.Show(); surface.Place(monitor, level,_noticeAnchor());
    }
    public void RefreshPlacement()
    {
        if(_disposed || !_surfaces.Any(s=>s.Surface==InterventionSurface.Notice))return;
        try
        {
            var monitors=DisplayMonitorProvider.GetMonitors();if(monitors.Count==0)return;
            var anchor=_noticeAnchor();
            var monitor=WindowPlacement.SelectMonitor(monitors,anchor);
            foreach(var surface in _surfaces.Where(s=>s.Surface==InterventionSurface.Notice))surface.Place(monitor,_displayed,anchor);
        }
        catch(Exception){Fail();}
    }
    public void ReturnToStudy()
    {
        HideAll();
        if (_study is null || !_actions.TryActivate(_study, _interventionTarget)) _showMain();
    }
    private string? ReturnProcess => _study is not null && _study.Handle != _interventionTarget?.Handle ? _study.ProcessName : null;
    private void HideAll()
    {
        var closing = _surfaces.ToArray(); _surfaces.Clear(); _displayed = InterventionLevel.None;
        foreach (var surface in closing) surface.ClosePermanently();
    }
    private void DisplayChanged(object? sender, EventArgs e)
    {
        if (_disposed || _dispatcher.HasShutdownStarted) return;
        _ = _dispatcher.BeginInvoke(new Action(() =>
        {
            if (_disposed || _surfaces.Count == 0 || _updating) return;
            _updating = true;
            try { Rebuild(_displayed); } catch (Exception) { Fail(); } finally { _updating = false; }
        }));
    }
    private void Fail()
    { HideAll(); _failed = true; Failed?.Invoke(this, "提醒显示失败，请重新开始会话"); _showMain(); }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; SystemEvents.DisplaySettingsChanged -= DisplayChanged; HideAll();
    }
}
