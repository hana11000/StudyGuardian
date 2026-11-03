using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using StudyGuardian.Core;
using StudyGuardian.Monitor;

namespace StudyGuardian.App;

internal enum InterventionSurface { Notice, Frame, Block }

internal sealed class InterventionWindow : Window
{
    private readonly InterventionSurface _surface;
    private readonly InterventionOptions _options;
    private readonly Border _border = new();
    private readonly TextBlock _heading = new(), _goal = new(), _detail = new(), _lock = new();
    private readonly Button _return = new();
    private readonly Button _appeal=new(){Content="申诉"},_emergency=new();
    private EscalationSnapshot? _state;
    private bool _allowClose;
    public event EventHandler? ReturnRequested;
    public event EventHandler? AppealRequested;
    public event EventHandler? EmergencyRequested;
    public long Handle => new WindowInteropHelper(this).Handle.ToInt64();

    public InterventionWindow(InterventionSurface surface, InterventionOptions options, bool activate)
    {
        _surface = surface; _options = options;
        Title = "Study Guardian · " + (surface == InterventionSurface.Block ? "拦截" : "提醒");
        Style = (Style)Application.Current.FindResource(typeof(Window));
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; Topmost = true; ShowActivated = activate;
        Width = surface == InterventionSurface.Notice ? 380 : 600;
        Height = surface == InterventionSurface.Notice ? double.NaN : 400;
        SizeToContent = surface == InterventionSurface.Notice ? SizeToContent.Height : SizeToContent.Manual;
        SourceInitialized += (_, _) =>
        {
            if (surface != InterventionSurface.Block) NativeWindowActions.ConfigurePassiveWindow((nint)Handle, surface == InterventionSurface.Frame);
        };
        Closing += OnClosing;
        _border.BorderBrush = Brush("DangerBrush");
        if (surface == InterventionSurface.Frame)
        {
            _border.Background = Brushes.Transparent; Content = _border; return;
        }
        var block = surface == InterventionSurface.Block;
        _border.Background = block ? new SolidColorBrush(Color.FromRgb(15, 17, 21)) { Opacity = options.OverlayOpacity } : Brush("CardBrush");
        _border.CornerRadius = new(block ? 0 : 12); _border.Padding = new(block ? 32 : 20);
        _border.BorderThickness = new(block ? 0 : 1);
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = block ? HorizontalAlignment.Center : HorizontalAlignment.Stretch, MaxWidth = block ? 640 : double.PositiveInfinity };
        _heading.FontSize = block ? 32 : 18; _heading.FontWeight = FontWeights.SemiBold;
        _heading.Foreground = block ? Brush("TextBrush") : Brush("AmberBrush");
        _goal.FontSize = block ? 20 : 14; _goal.Margin = new(0, 14, 0, 8);
        _goal.TextWrapping = TextWrapping.Wrap; _goal.MaxHeight = block ? 110 : 60; _goal.TextTrimming = TextTrimming.CharacterEllipsis;
        _detail.Foreground = Brush("MutedBrush"); _detail.Margin = new(0, 0, 0, 18);
        _detail.TextTrimming = TextTrimming.CharacterEllipsis;
        _return.Style = (Style)Application.Current.FindResource("PrimaryButton");
        _return.HorizontalAlignment = block ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _return.IsDefault = block; _return.Click += (_, _) => ReturnRequested?.Invoke(this, EventArgs.Empty);
        _lock.Foreground = Brush("MutedBrush"); _lock.FontFamily = new("Consolas");
        _lock.HorizontalAlignment = block ? HorizontalAlignment.Center : HorizontalAlignment.Left; _lock.Margin = new(0, 14, 0, 0);
        if (block) { _heading.TextAlignment = _goal.TextAlignment = _detail.TextAlignment = TextAlignment.Center; }
        panel.Children.Add(_heading); panel.Children.Add(_goal); panel.Children.Add(_detail); panel.Children.Add(_return); panel.Children.Add(_lock);
        var tools=new WrapPanel {Margin=new(0,12,0,0),HorizontalAlignment=block?HorizontalAlignment.Center:HorizontalAlignment.Left};
        _appeal.Margin=new(0,0,8,0);_appeal.Click+=(_,_)=>AppealRequested?.Invoke(this,EventArgs.Empty);_emergency.Click+=(_,_)=>EmergencyRequested?.Invoke(this,EventArgs.Empty);
        tools.Children.Add(_appeal);tools.Children.Add(_emergency);panel.Children.Add(tools);
        _border.Child = panel; Content = _border;
    }

    public void Update(string goal, EscalationSnapshot state, string? studyProcess,SessionToolsSnapshot tools,bool canAppeal)
    {
        _state = state;
        if (_surface == InterventionSurface.Frame) return;
        _heading.Text = _surface == InterventionSurface.Block ? "先回到学习。" : state.SupportiveOvertime ? "辅助时间到了" :
            state.Level == InterventionLevel.Warning ? "即将拦截" : "回到学习吧";
        _heading.Foreground = Brush(state.Level >= InterventionLevel.Warning && _surface != InterventionSurface.Block ? "DangerBrush" : "TextBrush");
        _border.BorderBrush = Brush(state.Level >= InterventionLevel.Warning ? "DangerBrush" : "AmberBrush");
        _goal.Text = goal.ReplaceLineEndings(" ");
        _detail.Text = _surface == InterventionSurface.Block ? $"本次已分心 {ActivityRow.Countdown(state.TotalDistraction)}" :
            state.Level == InterventionLevel.Warning ? $"已分心 {Math.Ceiling(state.EpisodeDuration.TotalSeconds):0} 秒 · {Math.Ceiling(state.UntilBlock.TotalSeconds):0} 秒后拦截" : state.Window?.Title;
        _return.Content = string.IsNullOrWhiteSpace(studyProcess) ? "回到学习" : "回到 " + System.IO.Path.GetFileNameWithoutExtension(studyProcess);
        _lock.Visibility = _surface == InterventionSurface.Block && state.LockRemaining > TimeSpan.Zero ? Visibility.Visible : Visibility.Collapsed;
        _lock.Text = "锁定 " + ActivityRow.Countdown(state.LockRemaining);
        var unlocked=_surface!=InterventionSurface.Block || state.LockRemaining==TimeSpan.Zero;
        _appeal.Visibility=unlocked?Visibility.Visible:Visibility.Collapsed;_appeal.IsEnabled=canAppeal;
        _appeal.Content=state.Level==InterventionLevel.Reminder?"这是学习需要":"申诉";
        _appeal.ToolTip=canAppeal?null:"请先配置 AI";
        _emergency.Visibility=_surface==InterventionSurface.Block && unlocked?Visibility.Visible:Visibility.Collapsed;
        _emergency.Content=$"应急通行（{tools.EmergencyUsesRemaining}）";_emergency.IsEnabled=tools.EmergencyUsesRemaining>0 && tools.EmergencyRemaining==TimeSpan.Zero;
    }

    public void Place(DisplayMonitor monitor, InterventionLevel level)
    {
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        if (_surface is InterventionSurface.Frame or InterventionSurface.Block)
        {
            NativeWindowActions.SetPhysicalBounds((nint)Handle, monitor.Bounds);
            dpi = VisualTreeHelper.GetDpi(this);
            if (_surface == InterventionSurface.Frame) _border.BorderThickness = new(_options.FrameThicknessPixels / dpi.DpiScaleX, _options.FrameThicknessPixels / dpi.DpiScaleY,
                _options.FrameThicknessPixels / dpi.DpiScaleX, _options.FrameThicknessPixels / dpi.DpiScaleY);
            return;
        }
        var work = monitor.WorkArea;
        var margin = (int)Math.Ceiling(16 * dpi.DpiScaleX);
        var width = level == InterventionLevel.Warning ? Math.Max(1, work.Width - margin * 2) : Math.Min((int)Math.Ceiling(Width * dpi.DpiScaleX), work.Width);
        var height = Math.Min((int)Math.Ceiling(ActualHeight * dpi.DpiScaleY), work.Height);
        var left = level == InterventionLevel.Warning ? work.Left + margin : work.Left + Math.Max(0, work.Width - width - margin);
        var top = level == InterventionLevel.Warning ? work.Top + margin : work.Top + Math.Max(0, work.Height - height - margin);
        NativeWindowActions.SetPhysicalBounds((nint)Handle, new(left, top, width, Math.Max(1, height)));
    }

    public void ClosePermanently() { _allowClose = true; Close(); }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_surface == InterventionSurface.Block && _state?.LockRemaining > TimeSpan.Zero) return;
        _ = Dispatcher.BeginInvoke(new Action(() => ReturnRequested?.Invoke(this, EventArgs.Empty)));
    }
    private static Brush Brush(string key) => (Brush)Application.Current.FindResource(key);
}
