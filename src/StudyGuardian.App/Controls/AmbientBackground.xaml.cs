using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StudyGuardian.App.Controls;

public enum AmbientState { Focus, Warn, Block, Idle }

public partial class AmbientBackground : UserControl
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(nameof(State), typeof(AmbientState), typeof(AmbientBackground),
        new PropertyMetadata(AmbientState.Focus, (d,_) => ((AmbientBackground)d).ApplyPalette(true)), v => v is AmbientState state && Enum.IsDefined(state));
    public static readonly DependencyProperty IsDarkProperty = DependencyProperty.Register(nameof(IsDark), typeof(bool), typeof(AmbientBackground),
        new PropertyMetadata(false, (d,_) => ((AmbientBackground)d).ApplyPalette(true)));
    public static readonly DependencyProperty MotionEnabledProperty = DependencyProperty.Register(nameof(MotionEnabled), typeof(bool), typeof(AmbientBackground),
        new PropertyMetadata(true, (d,_) => ((AmbientBackground)d).SyncMotion()));
    private static readonly DependencyPropertyKey IsAnimatingPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsAnimating), typeof(bool), typeof(AmbientBackground), new PropertyMetadata(false));
    public static readonly DependencyProperty IsAnimatingProperty = IsAnimatingPropertyKey.DependencyProperty;
    public AmbientState State { get => (AmbientState)GetValue(StateProperty); set => SetValue(StateProperty,value); }
    public bool IsDark { get => (bool)GetValue(IsDarkProperty); set => SetValue(IsDarkProperty,value); }
    public bool MotionEnabled { get => (bool)GetValue(MotionEnabledProperty); set => SetValue(MotionEnabledProperty,value); }
    public bool IsAnimating { get => (bool)GetValue(IsAnimatingProperty); private set => SetValue(IsAnimatingPropertyKey,value); }
    private readonly SolidColorBrush[] _colors = [new(),new(),new()];
    private bool _ready, _mounted;

    public AmbientBackground()
    {
        InitializeComponent();
        GlowA.Fill=_colors[0];GlowB.Fill=_colors[1];GlowC.Fill=_colors[2];_ready=true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_,_) => SyncMotion();
    }
    private void OnLoaded(object sender,RoutedEventArgs e)
    {
        if (!_mounted) SystemParameters.StaticPropertyChanged += SystemMotionChanged;
        _mounted=true;ApplyPalette(false);SyncMotion();
    }
    private void OnUnloaded(object sender,RoutedEventArgs e)
    {
        if (_mounted) SystemParameters.StaticPropertyChanged -= SystemMotionChanged;
        _mounted=false;SyncMotion();
    }
    private void SystemMotionChanged(object? sender,PropertyChangedEventArgs e)
    {
        if (e.PropertyName==nameof(SystemParameters.ClientAreaAnimation))
        {
            if (Dispatcher.CheckAccess()) SyncMotion();
            else if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(SyncMotion);
        }
    }
    private void ApplyPalette(bool animate)
    {
        if (!_ready || TryFindResource($"M8.{State}.A") is not Color first || TryFindResource($"M8.{State}.B") is not Color second) return;
        LightField.Opacity=(double)FindResource(IsDark?"M8.Glow.DarkOpacity":"M8.Glow.LightOpacity");
        Color[] targets=[first,second,second];
        for (var i=0;i<_colors.Length;i++)
        {
            var current=_colors[i].Color;
            _colors[i].BeginAnimation(SolidColorBrush.ColorProperty,null);
            _colors[i].Color=targets[i];
            if (animate && IsAnimating)
            {
                var change=new ColorAnimation(current,targets[i],(Duration)FindResource("M8.Motion.State"))
                {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseInOut},FillBehavior=FillBehavior.Stop};
                Timeline.SetDesiredFrameRate(change,(int)FindResource("M8.Motion.FrameRate"));
                _colors[i].BeginAnimation(SolidColorBrush.ColorProperty,change);
            }
        }
    }
    private void SyncMotion()
    {
        if (!_ready) return;
        var shouldRun=_mounted && IsVisible && MotionEnabled && SystemParameters.ClientAreaAnimation;
        if (shouldRun==IsAnimating && shouldRun) return;
        foreach (var transform in new[]{DriftA,DriftB,DriftC})
        {
            transform.BeginAnimation(TranslateTransform.XProperty,null);
            transform.BeginAnimation(TranslateTransform.YProperty,null);
        }
        IsAnimating=shouldRun;
        ApplyPalette(false);
        if (!shouldRun) return;
        StartDrift(DriftA,"M8.Motion.DriftA",1);
        StartDrift(DriftB,"M8.Motion.DriftB",-1);
        StartDrift(DriftC,"M8.Motion.DriftC",1);
    }
    private void StartDrift(TranslateTransform transform,string durationKey,int direction)
    {
        var cycle=(Duration)FindResource(durationKey);
        var duration=new Duration(TimeSpan.FromTicks(cycle.TimeSpan.Ticks/2));
        DoubleAnimation Drift(string distance) => new(-direction*(double)FindResource(distance),direction*(double)FindResource(distance),duration)
        {AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new CubicEase {EasingMode=EasingMode.EaseInOut}};
        var x=Drift("M8.Glow.DriftX");var y=Drift("M8.Glow.DriftY");
        Timeline.SetDesiredFrameRate(x,(int)FindResource("M8.Motion.FrameRate"));Timeline.SetDesiredFrameRate(y,(int)FindResource("M8.Motion.FrameRate"));
        transform.BeginAnimation(TranslateTransform.XProperty,x);
        transform.BeginAnimation(TranslateTransform.YProperty,y);
    }
}
