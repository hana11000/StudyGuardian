using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using StudyGuardian.Core;
using StudyGuardian.Data;

namespace StudyGuardian.App;

public partial class MainWindow : Window
{
    private readonly SessionController _controller;
    private readonly SessionUiOptions _sessionOptions;
    private readonly int _recentLimit;
    private readonly ObservableCollection<ActivityRow> _rows = [];
    private FloatingWindow? _floating;
    private Task _operation = Task.CompletedTask;
    private bool _closing;
    private bool _allowClose;
    private bool _sessionVisible;
    private readonly AiSettingsStore _aiSettings;
    private CancellationTokenSource? _generationCancellation;
    private bool _generating;
    private InterventionPresenter _interventions;
    private readonly UserSettingsStore _preferences;
    private readonly MonitoringOptions _monitoringOptions;
    private bool _appealing;

    public MainWindow(MonitoringOptions options, SessionUiOptions sessionOptions, AiOptions aiOptions, InterventionOptions interventionOptions, BrowserOptions browserOptions,SessionToolsOptions toolsOptions,string databasePath)
    {
        InitializeComponent();
        _sessionOptions = sessionOptions;_monitoringOptions=options;
        _preferences=new(Path.GetDirectoryName(databasePath)!,interventionOptions);
        _recentLimit = options.RecentEventLimit;
        _aiSettings = new(Path.GetDirectoryName(databasePath)!, aiOptions);
        _controller = new(options, sessionOptions, aiOptions, _aiSettings, interventionOptions, browserOptions,toolsOptions,_preferences,databasePath, Dispatcher);
        _interventions = CreateInterventions();
        _controller.ReturnRequested += (_, _) => _interventions.ReturnToStudy();
        SetupControl.Initialize(sessionOptions, aiOptions);
        SetupControl.GeneratePolicyRequested += GeneratePolicyRequested;
        _controller.StateChanged += (_, _) => UpdateState();
        _controller.SegmentSaved += (_, segment) => ShowSavedSegment(segment);
        RecordsGrid.ItemsSource = _rows;
        DataFolderButton.ToolTip = databasePath;
        UpdateCount();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _operation = _controller.InitializeAsync();
        await _operation;
        if ((_aiSettings.LoadError ?? _preferences.LoadError) is { } error) { NoticeText.Text = error; NoticeBorder.Visibility = Visibility.Visible; }
    }
    private void SettingsClick(object sender, RoutedEventArgs e)
    { new SettingsWindow(_aiSettings, _controller,_preferences) { Owner = this }.ShowDialog();_interventions.Dispose();_interventions=CreateInterventions();UpdateState(); }
    private InterventionPresenter CreateInterventions()
    {
        var presenter=new InterventionPresenter(_preferences.Current.Intervention,new(_monitoringOptions.ProtectedProcesses,Environment.ProcessId,_monitoringOptions.MaximumTitleCharacters),Dispatcher,ShowMain);
        presenter.Failed+=(_,message)=>{NoticeText.Text=message;NoticeBorder.Visibility=Visibility.Visible;};
        presenter.AppealRequested+=(_,target)=>ShowAppeal(target);
        presenter.EmergencyRequested+=(_,_)=>_controller.UseEmergencyPass();
        return presenter;
    }
    private void BreakClick(object sender,RoutedEventArgs e) => _controller.BeginBreak();
    private void EmergencyClick(object sender,RoutedEventArgs e) => _controller.UseEmergencyPass();
    private void AppealClick(object sender,RoutedEventArgs e) {if(_controller.AppealTarget is { } target) ShowAppeal(target);}
    private void ShowAppeal(WindowSnapshot target)
    {
        if(_appealing || !_controller.CanAppeal || _closing) return;
        _appealing=true;UpdateState();
        try {new AppealWindow(target,_controller.MaximumAppealCharacters,_controller.AppealMinutes,(reason,ct)=>_controller.AppealAsync(target,reason,ct)) {Owner=this}.ShowDialog();}
        finally {_appealing=false;UpdateState();}
    }
    private async void ReportClick(object sender,RoutedEventArgs e) => await ShowReportAsync();
    private async Task ShowReportAsync(string? selectedId=null)
    {
        try {var sessions=await _controller.ReadReportsAsync();if(!_closing) new ReportWindow(sessions,_controller.ReadReportAsync,selectedId) {Owner=this}.Show();}
        catch(Exception) {NoticeText.Text="报告读取失败";NoticeBorder.Visibility=Visibility.Visible;}
    }
    private async void GeneratePolicyRequested(object? sender, EventArgs e)
    {
        if (_closing || _generating) return;
        _generationCancellation = new();
        _operation = GeneratePolicyAsync(_generationCancellation.Token);
        await _operation;
        _generationCancellation.Dispose(); _generationCancellation = null;
    }
    private async Task GeneratePolicyAsync(CancellationToken cancellationToken)
    {
        _generating = true; SetupControl.SetGenerating(true); UpdateState();
        try { SetupControl.ApplyPolicy(await _controller.GeneratePolicyAsync(SetupControl.Goal, cancellationToken)); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (AiUnavailableException ex) { SetupControl.ShowPolicyError(ex.Message); }
        finally { _generating = false; SetupControl.SetGenerating(false); UpdateState(); }
    }
    private async void StartRequested(object? sender, EventArgs e)
    {
        if (_closing || !SetupControl.TryGetOptions(out var options)) return;
        _operation = _controller.StartAsync(options!);
        await _operation;
        if (_closing || !_controller.Snapshot.IsRunning) return;
        _sessionVisible = true;
        ShowFloating();
        WindowState = WindowState.Minimized;
    }
    private async void PauseClick(object? sender, RoutedEventArgs e) => await TogglePauseAsync();
    private Task TogglePauseAsync() => _controller.SetPausedAsync(!_controller.Snapshot.IsManuallyPaused);
    private async void StopClick(object sender, RoutedEventArgs e)
    {
        _operation = _controller.StopAsync();
        await _operation;
    }
    private void FloatClick(object sender, RoutedEventArgs e)
    {
        ShowFloating();
        WindowState = WindowState.Minimized;
    }
    private void ShowFloating()
    {
        if (!_controller.Snapshot.IsRunning) return;
        if (_floating is null)
        {
            _floating = new(_sessionOptions.FloatingOpacity);
            _floating.PauseRequested += async (_, _) => await TogglePauseAsync();
            _floating.BreakRequested += (_,_)=>_controller.BeginBreak();
            _floating.OpenMainRequested += (_, _) => ShowMain();
        }
        _floating.Update(_controller.Snapshot, _controller.Options!.Goal, _controller.Escalation,_controller.IsOnBreak?_controller.Tools.BreakRemaining:null,_controller.CanUseTools);
        _floating.Show();
    }
    private void ShowMain()
    {
        if (_closing) return;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
    private void UpdateState()
    {
        var state = _controller.Snapshot;
        var active = _controller.HasSession;
        if (!_closing) _interventions.Update(state, _controller.Escalation, _controller.Options?.Policy?.GoalSummary ?? _controller.Options?.Goal ?? "", _controller.LastStudyWindow,_controller.Tools,_controller.CanAppeal);
        SetupControl.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        StartButton.Visibility = active || HistoryPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        RunningPanel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        SetupControl.IsEnabled = _controller.IsReady && !_controller.IsBusy && !_controller.IsFaulted && !_closing && !_generating;
        SettingsButton.IsEnabled = SetupControl.IsEnabled && !active;
        AiStatusText.Text = _controller.AiStatus;
        StartButton.IsEnabled = SetupControl.IsEnabled;
        PauseButton.IsEnabled = FloatButton.IsEnabled = StopButton.IsEnabled = state.IsRunning && !_controller.IsBusy && !_controller.IsFaulted && !_closing;
        DataFolderButton.IsEnabled = _controller.IsReady;
        ReportButton.IsEnabled=_controller.IsReady && !_closing;
        BreakButton.IsEnabled=_controller.CanUseTools;
        AppealButton.IsEnabled=_controller.CanAppeal && !_appealing;
        AppealButton.ToolTip=_controller.CanAppeal?null:"需要已判定的窗口和 AI 设置";
        EmergencyButton.IsEnabled=_controller.CanUseTools && _controller.Tools.EmergencyUsesRemaining>0 && _controller.Tools.EmergencyRemaining==TimeSpan.Zero;
        EmergencyButton.Content=$"应急通行（{_controller.Tools.EmergencyUsesRemaining}）";
        ToolsText.Text=_controller.Tools.EmergencyRemaining>TimeSpan.Zero?"通行 "+ActivityRow.Countdown(_controller.Tools.EmergencyRemaining):"";
        if (_controller.Options is { } options)
        {
            GoalText.Text = options.Goal;
            GoalText.ToolTip = options.Goal;
            StatusText.Text = ActivityRow.DisplayState(state);
            StatusDot.Fill = ActivityRow.BrushForState(state, _controller.Escalation);
            TimeText.Text = ActivityRow.Countdown(_controller.IsOnBreak?_controller.Tools.BreakRemaining:state.Remaining);
            FocusText.Text = "专注率 " + (state.FocusRate is { } rate ? rate.ToString("P0") : "—");
            ProcessText.Text = state.Window?.ProcessName ?? "—";
            WindowTitleText.Text = state.Window?.Title ?? "—";
            WindowTitleText.ToolTip = state.Window?.Title;
            PauseButton.Content = _controller.IsOnBreak?"结束休息":state.IsManuallyPaused ? "继续" : "暂停";
            _floating?.Update(state, options.Goal, _controller.Escalation,_controller.IsOnBreak?_controller.Tools.BreakRemaining:null,_controller.CanUseTools);
        }
        SavedText.Text = _controller.IsBusy ? "保存中…" : _controller.LastSavedAt is { } saved ? "已保存 · " + saved.ToString("HH:mm:ss") : "";
        if (_controller.IsFaulted)
        {
            NoticeBorder.Visibility = Visibility.Visible;
            NoticeText.Text = _controller.ErrorMessage;
            NoticeText.ToolTip = _controller.ErrorDetail;
        }
        if ((!active || _controller.IsFaulted) && _sessionVisible)
        {
            _sessionVisible = false;
            _floating?.ClosePermanently();
            _floating = null;
            ShowMain();
            if(!_closing && !_controller.IsFaulted && _controller.LastCompletedSessionId is { } completed) _=ShowReportAsync(completed);
        }
    }
    private void ShowSavedSegment(ActivitySegment segment)
    {
        var existing = _rows.Select((row, index) => (row, index)).FirstOrDefault(item => item.row.Id == segment.Id);
        if (existing.row is not null) _rows[existing.index] = new(segment);
        else _rows.Insert(0, new(segment));
        while (_rows.Count > _recentLimit) _rows.RemoveAt(_rows.Count - 1);
        UpdateCount();
    }
    private void UpdateCount()
    {
        RecordsHint.Text = $"{_rows.Count} 条";
        EmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void HistoryClick(object sender, RoutedEventArgs e)
    {
        var showHistory = HistoryPanel.Visibility != Visibility.Visible;
        HistoryPanel.Visibility = showHistory ? Visibility.Visible : Visibility.Collapsed;
        MainContent.Visibility = showHistory ? Visibility.Collapsed : Visibility.Visible;
        HistoryButton.Content = showHistory ? "返回" : "活动记录";
        UpdateState();
    }
    private void OpenDataFolderClick(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(Path.GetDirectoryName(_controller.DatabasePath)!) { UseShellExecute = true }); }
        catch (Exception ex)
        {
            NoticeBorder.Visibility = Visibility.Visible;
            NoticeText.Text = "无法打开记录文件夹";
            NoticeText.ToolTip = ex.Message;
        }
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        _interventions.Dispose();
        _generationCancellation?.Cancel();
        UpdateState();
        await _operation;
        await _controller.DisposeAsync();
        _floating?.ClosePermanently();
        _allowClose = true;
        Close();
    }
}
