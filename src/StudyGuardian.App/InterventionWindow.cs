using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using StudyGuardian.App.Controls;
using StudyGuardian.Core;
using StudyGuardian.Monitor;

namespace StudyGuardian.App;

internal enum InterventionSurface { Notice, Frame, Block }

internal sealed class InterventionWindow : Window
{
    private readonly InterventionSurface _surface;
    private readonly TextBlock _heading=new(),_goal=new(),_detail=new(),_lock=new();
    private readonly Button _return=new(),_appeal=new(){Content="申诉"},_emergency=new();
    private EscalationSnapshot? _state;
    private bool _allowClose;
    public InterventionSurface Surface=>_surface;
    public event EventHandler? ReturnRequested;
    public event EventHandler? AppealRequested;
    public event EventHandler? EmergencyRequested;
    public long Handle=>new WindowInteropHelper(this).Handle.ToInt64();

    public InterventionWindow(InterventionSurface surface,InterventionOptions options,bool activate)
    {
        _surface=surface;
        Title="Study Guardian · "+(surface==InterventionSurface.Block?"拦截":"提醒");
        Style=UiVisuals.Resource<Style>("M8.BareWindow");
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=surface==InterventionSurface.Frame;
        ShowInTaskbar=false;Topmost=true;ShowActivated=activate;
        Width=surface==InterventionSurface.Notice?UiVisuals.Resource<double>("M8.Notice.Width"):600;
        Height=surface==InterventionSurface.Notice?double.NaN:400;
        SizeToContent=surface==InterventionSurface.Notice?SizeToContent.Height:SizeToContent.Manual;
        SourceInitialized+=(_,_)=>{if(surface!=InterventionSurface.Block)NativeWindowActions.ConfigurePassiveWindow((nint)Handle,surface==InterventionSurface.Frame);};
        Closing+=OnClosing;
        if(surface==InterventionSurface.Frame){Content=new EdgeGlow();return;}
        FrostWindow.SetMaterial(this,WindowMaterial.Acrylic);
        FrostWindow.SetSquareCorners(this,surface==InterventionSurface.Block);
        var block=surface==InterventionSurface.Block;
        var surfaceRoot=new Grid();
        var ambient=new AmbientBackground {State=block?AmbientState.Block:AmbientState.Warn,MotionEnabled=false};
        ambient.SetBinding(AmbientBackground.IsDarkProperty,new Binding(nameof(ThemeResources.IsDark)){Source=ThemeResources.Current});
        surfaceRoot.Children.Add(ambient);
        var panel=new StackPanel {VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=block?HorizontalAlignment.Center:HorizontalAlignment.Stretch,MaxWidth=block?640:double.PositiveInfinity,Margin=UiVisuals.Resource<Thickness>("M8.Space.Card")};
        UiVisuals.SetAnimateIn(panel,true);
        _heading.SetResourceReference(StyleProperty,block?"M8.Title":"M8.Caption");
        _heading.SetResourceReference(TextBlock.ForegroundProperty,"M8.Text");
        _heading.FontWeight=FontWeights.SemiBold;_heading.TextTrimming=TextTrimming.CharacterEllipsis;
        _goal.SetResourceReference(TextBlock.FontSizeProperty,"M8.Type.Section");
        _goal.Margin=new(0,12,0,12);_goal.TextWrapping=TextWrapping.Wrap;_goal.MaxHeight=block?110:72;_goal.TextTrimming=TextTrimming.CharacterEllipsis;
        _detail.SetResourceReference(StyleProperty,"M8.Caption");_detail.Margin=new(0,0,0,24);_detail.TextTrimming=TextTrimming.CharacterEllipsis;
        _return.Style=UiVisuals.Resource<Style>("M8.PrimaryButton");_return.HorizontalAlignment=HorizontalAlignment.Left;
        _return.IsDefault=block;_return.Click+=(_,_)=>ReturnRequested?.Invoke(this,EventArgs.Empty);
        _lock.SetResourceReference(StyleProperty,"M8.Timer");_lock.SetResourceReference(TextBlock.FontSizeProperty,"M8.Type.LockTimer");
        _lock.HorizontalAlignment=HorizontalAlignment.Center;_lock.Margin=new(0,0,0,24);
        if(block){_heading.TextAlignment=_goal.TextAlignment=_detail.TextAlignment=TextAlignment.Center;_return.HorizontalAlignment=HorizontalAlignment.Center;}
        panel.Children.Add(_lock);panel.Children.Add(_heading);panel.Children.Add(_goal);panel.Children.Add(_detail);panel.Children.Add(_return);
        var tools=new WrapPanel {Margin=new(0,12,0,0),HorizontalAlignment=block?HorizontalAlignment.Center:HorizontalAlignment.Left};
        _appeal.Style=_emergency.Style=UiVisuals.Resource<Style>("M8.TextButton");
        _appeal.Click+=(_,_)=>AppealRequested?.Invoke(this,EventArgs.Empty);_emergency.Click+=(_,_)=>EmergencyRequested?.Invoke(this,EventArgs.Empty);
        tools.Children.Add(_appeal);tools.Children.Add(_emergency);panel.Children.Add(tools);
        surfaceRoot.Children.Add(panel);
        var shell=new Border {Child=surfaceRoot};
        if(!block){shell.CornerRadius=UiVisuals.Resource<CornerRadius>("M8.Radius.Card");UiVisuals.SetCornerRadius(surfaceRoot,shell.CornerRadius);shell.SetResourceReference(Border.BorderBrushProperty,"M8.GlassEdge");shell.BorderThickness=new(1);}
        var style=new Style(typeof(Border));
        style.Setters.Add(new Setter(Border.BackgroundProperty,new DynamicResourceExtension("M8.Background")));
        var backdrop=new DataTrigger {Binding=new Binding {Path=new PropertyPath(FrostWindow.HasBackdropProperty),Source=this},Value=true};
        backdrop.Setters.Add(new Setter(Border.BackgroundProperty,new DynamicResourceExtension("M8.WindowTint")));style.Triggers.Add(backdrop);
        shell.Style=style;Content=shell;
    }

    public void Update(string goal,EscalationSnapshot state,string? studyProcess,SessionToolsSnapshot tools,bool canAppeal)
    {
        _state=state;
        if(_surface==InterventionSurface.Frame)return;
        var block=_surface==InterventionSurface.Block;
        _heading.Text=block?"先回到学习。":state.SupportiveOvertime?"辅助时间到了":System.IO.Path.GetFileNameWithoutExtension(state.Window?.ProcessName??"")+" · "+state.Window?.Title;
        _goal.Text=block?"这段时间要学的是 "+goal.ReplaceLineEndings(" "):"这和「"+goal.ReplaceLineEndings(" ")+"」无关";
        _detail.Text=block?$"本次已分心 {ActivityRow.Countdown(state.TotalDistraction)}":state.Level==InterventionLevel.Warning?$"{Math.Ceiling(state.UntilBlock.TotalSeconds):0} 秒后拦截":"";
        _detail.Visibility=string.IsNullOrEmpty(_detail.Text)?Visibility.Collapsed:Visibility.Visible;
        _return.Content=string.IsNullOrWhiteSpace(studyProcess)?"回到学习":"回到 "+System.IO.Path.GetFileNameWithoutExtension(studyProcess);
        _lock.Visibility=block?Visibility.Visible:Visibility.Collapsed;_lock.Text=ActivityRow.Countdown(state.LockRemaining);
        var unlocked=!block || state.LockRemaining==TimeSpan.Zero;
        _appeal.Visibility=unlocked?Visibility.Visible:Visibility.Collapsed;_appeal.IsEnabled=canAppeal;
        _appeal.Content=state.Level==InterventionLevel.Reminder?"学习需要":"申诉";_appeal.ToolTip=canAppeal?null:"请先配置 AI";
        _emergency.Visibility=block && unlocked?Visibility.Visible:Visibility.Collapsed;
        _emergency.Content=$"应急通行 {tools.EmergencyUsesRemaining}";_emergency.IsEnabled=tools.EmergencyUsesRemaining>0 && tools.EmergencyRemaining==TimeSpan.Zero;
    }

    public void Place(DisplayMonitor monitor,InterventionLevel level,Rect? anchor=null)
    {
        UpdateLayout();
        if(_surface is InterventionSurface.Frame or InterventionSurface.Block){NativeWindowActions.SetPhysicalBounds((nint)Handle,monitor.Bounds);return;}
        var dpi=VisualTreeHelper.GetDpi(this);var work=monitor.WorkArea;
        var bounds=NoticePlacement.Calculate(new Rect(work.Left,work.Top,work.Width,work.Height),new Size(Width*dpi.DpiScaleX,Math.Max(1,ActualHeight*dpi.DpiScaleY)),anchor,UiVisuals.Resource<double>("M8.Notice.Gap")*dpi.DpiScaleX);
        NativeWindowActions.SetPhysicalBounds((nint)Handle,new((int)bounds.Left,(int)bounds.Top,(int)bounds.Width,(int)bounds.Height));
    }
    public void ClosePermanently(){_allowClose=true;Close();}
    private void OnClosing(object? sender,CancelEventArgs e)
    {
        if(_allowClose)return;e.Cancel=true;
        if(_surface==InterventionSurface.Block && _state?.LockRemaining>TimeSpan.Zero)return;
        _=Dispatcher.BeginInvoke(new Action(()=>ReturnRequested?.Invoke(this,EventArgs.Empty)));
    }
}
