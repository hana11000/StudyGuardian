using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace StudyGuardian.App.Controls;

public sealed class StatusLight : Grid
{
    public static readonly DependencyProperty StateProperty=DependencyProperty.Register(nameof(State),typeof(AmbientState),typeof(StatusLight),new PropertyMetadata(AmbientState.Focus,(d,_)=>((StatusLight)d).Refresh()));
    public static readonly DependencyProperty MotionEnabledProperty=DependencyProperty.Register(nameof(MotionEnabled),typeof(bool),typeof(StatusLight),new PropertyMetadata(true,(d,_)=>((StatusLight)d).Refresh()));
    public AmbientState State {get=>(AmbientState)GetValue(StateProperty);set=>SetValue(StateProperty,value);}
    public bool MotionEnabled {get=>(bool)GetValue(MotionEnabledProperty);set=>SetValue(MotionEnabledProperty,value);}
    private readonly Ellipse _halo=new(),_dot=new();
    private bool _loaded;
    public StatusLight()
    {
        Width=Height=24;IsHitTestVisible=false;
        _halo.Width=_halo.Height=24;_halo.Opacity=UiVisuals.Resource<double>("M8.Dot.Dim");
        _halo.Effect=new BlurEffect {Radius=UiVisuals.Resource<double>("M8.Dot.Blur")};_halo.CacheMode=new BitmapCache();
        _dot.Width=_dot.Height=7;Children.Add(_halo);Children.Add(_dot);
        Loaded+=OnLoaded;Unloaded+=OnUnloaded;IsVisibleChanged+=(_,_)=>Refresh();Refresh();
    }
    private void OnLoaded(object sender,RoutedEventArgs e){if(!_loaded)SystemParameters.StaticPropertyChanged+=SystemChanged;_loaded=true;Refresh();}
    private void OnUnloaded(object sender,RoutedEventArgs e){SystemParameters.StaticPropertyChanged-=SystemChanged;_loaded=false;Refresh();}
    private void SystemChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName==nameof(SystemParameters.ClientAreaAnimation) && !Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(Refresh);}
    private void Refresh()
    {
        _dot.SetResourceReference(Shape.FillProperty,$"M8.{State}Dot");_halo.SetResourceReference(Shape.FillProperty,$"M8.{State}Dot");
        _halo.BeginAnimation(OpacityProperty,null);
        if(!_loaded || !IsVisible || !MotionEnabled || State!=AmbientState.Focus || !SystemParameters.ClientAreaAnimation)return;
        var motion=new DoubleAnimation(UiVisuals.Resource<double>("M8.Dot.Dim"),UiVisuals.Resource<double>("M8.Dot.Bright"),UiVisuals.Resource<Duration>("M8.Motion.BreathHalfCycle"))
        {AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new CubicEase {EasingMode=EasingMode.EaseInOut}};
        Timeline.SetDesiredFrameRate(motion,UiVisuals.Resource<int>("M8.Motion.FrameRate"));_halo.BeginAnimation(OpacityProperty,motion);
    }
}
