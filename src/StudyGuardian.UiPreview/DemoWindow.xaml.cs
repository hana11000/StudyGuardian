using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using StudyGuardian.App.Controls;

namespace StudyGuardian.UiPreview;

public partial class DemoWindow : Window
{
    private bool _ready;
    private ResourceDictionary? _renderResources;
    public DemoWindow()
    {
        InitializeComponent(); _ready=true;SetTheme(false);SetState(AmbientState.Focus);
        StateChanged += (_,_) => RefreshMotion();
        IsVisibleChanged += (_,_) => RefreshMotion();
        Loaded += (_,_) => AnimateIntro();
    }
    public void SetTheme(bool dark,ResourceDictionary? renderResources=null)
    {
        _renderResources=renderResources;
        var resources=renderResources ?? Resources;
        resources.MergedDictionaries[1]=new ResourceDictionary {Source=new Uri($"pack://application:,,,/StudyGuardian.App;component/Themes/{(dark?"Dark":"Light")}.xaml")};
        LightChoice.IsChecked=!dark;DarkChoice.IsChecked=dark;
        foreach (var ambient in Descendants<AmbientBackground>(RootSurface)) ambient.IsDark=dark;
    }
    public void SetState(AmbientState state)
    {
        Ambient.State=state;
        (state switch {AmbientState.Focus=>FocusState,AmbientState.Warn=>WarnState,AmbientState.Block=>BlockState,_=>IdleState}).IsChecked=true;
        StateText.Text=state switch {AmbientState.Focus=>"专注中",AmbientState.Warn=>"提醒",AmbientState.Block=>"拦截",_=>"休息中"};
        StateDot.SetResourceReference(Shape.FillProperty,$"M8.{state}Dot");
        StateHalo.SetResourceReference(Shape.FillProperty,$"M8.{state}Dot");
        RefreshMotion();
    }
    public void SetMotion(bool enabled) { MotionChoice.IsChecked=enabled;RefreshMotion(); }
    private void ThemeChanged(object sender,RoutedEventArgs e) { if (_ready && sender is RadioButton {IsChecked:true} choice) SetTheme(choice==DarkChoice,_renderResources); }
    private void AmbientChoiceChanged(object sender,RoutedEventArgs e) { if (_ready && sender is RadioButton {Tag:string state}) SetState(Enum.Parse<AmbientState>(state)); }
    private void MotionChanged(object sender,RoutedEventArgs e) { if (_ready) RefreshMotion(); }
    private void RefreshMotion()
    {
        Ambient.MotionEnabled=MotionChoice.IsChecked==true && WindowState!=WindowState.Minimized && IsVisible;
    }
    private void AnimateIntro()
    {
        if(!SystemParameters.ClientAreaAnimation) return;
        RootSurface.RenderTransformOrigin=new Point(.5,.5);
        var scale=new ScaleTransform();RootSurface.RenderTransform=scale;
        var duration=(Duration)FindResource("M8.Motion.Appear");
        var appear=new DoubleAnimation((double)FindResource("M8.Motion.EnterScale"),1,duration)
        {EasingFunction=(IEasingFunction)FindResource("M8.Ease.Enter"),FillBehavior=FillBehavior.Stop};
        scale.BeginAnimation(ScaleTransform.ScaleXProperty,appear);scale.BeginAnimation(ScaleTransform.ScaleYProperty,appear);
        RootSurface.BeginAnimation(OpacityProperty,new DoubleAnimation(0,1,duration)
        {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
    }
    private void MinimizeClick(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    private void CloseClick(object sender,RoutedEventArgs e)=>Close();
    private void DragWindow(object sender,MouseButtonEventArgs e)
    {
        for(var node=e.OriginalSource as DependencyObject;node is not null;node=VisualTreeHelper.GetParent(node)) if(node is ButtonBase) return;
        if(e.LeftButton==MouseButtonState.Pressed) DragMove();
    }
    private void ClipSurface(object sender,SizeChangedEventArgs e)=>ApplyClip((FrameworkElement)sender,"M8.Radius.Window");
    private void ClipTile(object sender,SizeChangedEventArgs e)=>ApplyClip((FrameworkElement)sender,"M8.Radius.Card");
    private void ApplyClip(FrameworkElement surface,string token)
    {
        var radius=((CornerRadius)FindResource(token)).TopLeft;
        surface.Clip=new RectangleGeometry(new Rect(0,0,surface.ActualWidth,surface.ActualHeight),radius,radius);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T match) yield return match;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Descendants<T>(VisualTreeHelper.GetChild(root,i))) yield return child;
    }
}
