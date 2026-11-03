using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using StudyGuardian.Core;

namespace StudyGuardian.App.Controls;

public static class UiVisuals
{
    public static readonly DependencyProperty AnimateInProperty=DependencyProperty.RegisterAttached("AnimateIn",typeof(bool),typeof(UiVisuals),new PropertyMetadata(false,(d,e)=>
    {if(d is FrameworkElement element){element.Loaded-=AnimateLoaded;if((bool)e.NewValue)element.Loaded+=AnimateLoaded;}}));
    public static bool GetAnimateIn(DependencyObject d)=>(bool)d.GetValue(AnimateInProperty);
    public static void SetAnimateIn(DependencyObject d,bool value)=>d.SetValue(AnimateInProperty,value);
    private static void AnimateLoaded(object sender,RoutedEventArgs e)
    {
        if(!SystemParameters.ClientAreaAnimation)return;
        var element=(FrameworkElement)sender;var scale=new ScaleTransform();element.RenderTransform=scale;element.RenderTransformOrigin=new Point(.5,.5);
        var duration=Resource<Duration>("M8.Motion.Appear");
        var enter=new DoubleAnimation(Resource<double>("M8.Motion.EnterScale"),1,duration){EasingFunction=Resource<IEasingFunction>("M8.Ease.Enter"),FillBehavior=FillBehavior.Stop};
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,enter);scale.BeginAnimation(ScaleTransform.ScaleYProperty,enter);
        element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,duration){EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
    }
    public static readonly DependencyProperty CornerRadiusProperty=DependencyProperty.RegisterAttached("CornerRadius",typeof(CornerRadius),typeof(UiVisuals),new PropertyMetadata(new CornerRadius(),RadiusChanged));
    public static CornerRadius GetCornerRadius(DependencyObject d)=>(CornerRadius)d.GetValue(CornerRadiusProperty);
    public static void SetCornerRadius(DependencyObject d,CornerRadius v)=>d.SetValue(CornerRadiusProperty,v);
    private static void RadiusChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {if(d is FrameworkElement element){element.SizeChanged-=Clip;element.SizeChanged+=Clip;ApplyClip(element);}}
    private static void Clip(object sender,SizeChangedEventArgs e)=>ApplyClip((FrameworkElement)sender);
    private static void ApplyClip(FrameworkElement element)
    {var radius=GetCornerRadius(element).TopLeft;element.Clip=new RectangleGeometry(new Rect(0,0,element.ActualWidth,element.ActualHeight),radius,radius);}
    public static T Resource<T>(string key)=>(T)Application.Current.FindResource(key);
    public static TextBlock Text(string text,string style="M8.Caption")=>new(){Text=text,Style=Resource<Style>(style)};
    public static ContentControl Card(UIElement content,bool compact=false)=>new(){Content=content,Style=Resource<Style>("M8.GlassCard"),Padding=Resource<Thickness>(compact?"M8.Space.CompactCard":"M8.Space.Card")};
    public static AmbientState StateFor(StudySessionSnapshot state,EscalationSnapshot? escalation,TimeSpan? breakRemaining=null)
    {
        if(!state.IsRunning || state.PauseReason!=PauseReason.None || breakRemaining.HasValue)return AmbientState.Idle;
        if(state.Decision.CanEnforce && escalation is {IsDistracting:true,Level:InterventionLevel.Block} && state.Window==escalation.Window)return AmbientState.Block;
        return state.Decision.Verdict==StudyVerdict.OnTask?AmbientState.Focus:AmbientState.Warn;
    }
}
