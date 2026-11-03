using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using StudyGuardian.Core;
using StudyGuardian.App.Controls;
using System.Diagnostics;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace StudyGuardian.App;

public partial class FloatingWindow : Window
{
    private bool _allowClose, _expanded;
    private readonly double _restingOpacity;
    private readonly DispatcherTimer _idleTimer;
    private long _lastInteraction=Stopwatch.GetTimestamp();
    public event EventHandler? EndRequested;
    public event EventHandler? PauseRequested;
    public event EventHandler? OpenMainRequested;
    public event EventHandler? BreakRequested;
    public FloatingWindow(double opacity)
    {
        InitializeComponent();
        _restingOpacity=opacity;Opacity=opacity;
        _idleTimer=new DispatcherTimer(DispatcherPriority.Background,Dispatcher) {Interval=UiVisuals.Resource<Duration>("M8.Float.IdleTick").TimeSpan};
        _idleTimer.Tick+=(_,_)=>RefreshIdle();
        IsVisibleChanged+=(_,_)=>{if(IsVisible){_lastInteraction=Stopwatch.GetTimestamp();_idleTimer.Start();}else{_idleTimer.Stop();Light.MotionEnabled=false;}};
        StateChanged+=(_,_)=>{if(WindowState==WindowState.Minimized){_idleTimer.Stop();Light.MotionEnabled=false;}else if(IsVisible){_lastInteraction=Stopwatch.GetTimestamp();_idleTimer.Start();RefreshIdle();}};
        Closed+=(_,_)=>_idleTimer.Stop();
        MouseMove+=(_,_)=>{_lastInteraction=Stopwatch.GetTimestamp();RefreshIdle();};
        Loaded+=(_,_)=>{RefreshIdle();WindowPlacement.Constrain(this);};
        SizeChanged+=(_,_)=>WindowPlacement.Constrain(this);
        DpiChanged+=(_,_)=>WindowPlacement.Constrain(this);
        Left = SystemParameters.WorkArea.Right - Width - 24;
        Top = SystemParameters.WorkArea.Top + 24;
    }
    public void Update(StudySessionSnapshot snapshot, string goal, EscalationSnapshot? escalation = null,TimeSpan? breakRemaining=null,bool canBreak=true,string? aiStatus=null,string? aiDetail=null)
    {
        Light.State = UiVisuals.StateFor(snapshot,escalation,breakRemaining);
        StatusText.Text = ActivityRow.DisplayState(snapshot);
        AiText.Text = aiStatus;
        AiText.ToolTip = aiDetail;
        var warning=Light.State==AmbientState.Warn && snapshot.Decision.CanEnforce && escalation is {IsDistracting:true,Level:InterventionLevel.Warning} && snapshot.Window==escalation.Window;
        TimeText.Text = warning?$"{Math.Ceiling(escalation!.UntilBlock.TotalSeconds):0} 秒后拦截":ActivityRow.Countdown(breakRemaining ?? snapshot.Remaining);
        TimeText.SetResourceReference(TextBlock.FontSizeProperty,warning?"M8.Type.Caption":"M8.Float.TimerSize");
        GoalText.Text = goal.ReplaceLineEndings(" ");
        GoalText.ToolTip = goal;
        FocusText.Text = (snapshot.FocusRate is { } rate ? rate.ToString("P0") : "—");
        PauseButton.Content = snapshot.IsManuallyPaused || breakRemaining.HasValue ? "继续" : "暂停";
        PauseButton.ToolTip = breakRemaining.HasValue ? "结束休息" : snapshot.IsManuallyPaused ? "继续" : "暂停";
        BreakButton.IsEnabled=canBreak;
        AutomationProperties.SetName(PauseButton, (string)PauseButton.ToolTip);
    }
    private void EngagementChanged(object sender,RoutedEventArgs e)
    {
        _lastInteraction=Stopwatch.GetTimestamp();RefreshIdle();
        var expanded=IsMouseOver || IsKeyboardFocusWithin;
        if(expanded==_expanded)return;_expanded=expanded;
        ExpandedPanel.Visibility=expanded?Visibility.Visible:Visibility.Collapsed;
        var target=UiVisuals.Resource<double>(expanded?"M8.Float.ExpandedHeight":"M8.Float.CollapsedHeight");
        if(!SystemParameters.ClientAreaAnimation){BeginAnimation(HeightProperty,null);Height=target;return;}
        var motion=new DoubleAnimation(ActualHeight,target,UiVisuals.Resource<Duration>("M8.Motion.Expand"))
        {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
        Height=target;BeginAnimation(HeightProperty,motion);
    }
    private void RefreshIdle()
    {
        var faded=!IsMouseOver && !IsKeyboardFocusWithin && Stopwatch.GetElapsedTime(_lastInteraction)>=UiVisuals.Resource<Duration>("M8.Float.IdleDelay").TimeSpan;
        Opacity=faded?UiVisuals.Resource<double>("M8.Float.IdleOpacity"):_restingOpacity;
        Light.MotionEnabled=IsVisible && !faded;
    }
    private void EndClick(object sender,RoutedEventArgs e)=>EndRequested?.Invoke(this,EventArgs.Empty);
    public void FocusControls()
    {
        if(IsVisible){Activate();Surface.Focus();}
        FocusManager.SetFocusedElement(this,Surface);
    }
    public void ClosePermanently() { _allowClose = true; Close(); }
    private void PauseClick(object sender, RoutedEventArgs e) => PauseRequested?.Invoke(this, EventArgs.Empty);
    private void OpenMainClick(object sender, RoutedEventArgs e) => OpenMainRequested?.Invoke(this, EventArgs.Empty);
    private void HideClick(object sender,RoutedEventArgs e) => Hide();
    private void BreakClick(object sender,RoutedEventArgs e) => BreakRequested?.Invoke(this,EventArgs.Empty);
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
    }
    private void DragWindow(object sender, MouseButtonEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
            if (node is ButtonBase) return;
        if (e.ClickCount == 2) OpenMainRequested?.Invoke(this, EventArgs.Empty);
        else if (e.LeftButton == MouseButtonState.Pressed) {Surface.Focus();DragMove();WindowPlacement.Constrain(this);}
    }
}
