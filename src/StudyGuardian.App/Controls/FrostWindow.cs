using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace StudyGuardian.App.Controls;

public enum WindowMaterial { None, Mica=2, Acrylic=3 }

public static class FrostWindow
{
    public static readonly DependencyProperty MaterialProperty=DependencyProperty.RegisterAttached("Material",typeof(WindowMaterial),typeof(FrostWindow),new PropertyMetadata(WindowMaterial.None,MaterialChanged));
    public static readonly DependencyProperty IsDarkProperty=DependencyProperty.RegisterAttached("IsDark",typeof(bool),typeof(FrostWindow),new PropertyMetadata(false,(d,_)=> {if(d is Window w)Apply(w);}));
    public static readonly DependencyProperty StateProperty=DependencyProperty.RegisterAttached("State",typeof(AmbientState),typeof(FrostWindow),new PropertyMetadata(AmbientState.Idle));
    public static readonly DependencyProperty SquareCornersProperty=DependencyProperty.RegisterAttached("SquareCorners",typeof(bool),typeof(FrostWindow),new PropertyMetadata(false,(d,_)=>{if(d is Window w)Apply(w);}));
    public static readonly DependencyProperty ClipRadiusProperty=DependencyProperty.RegisterAttached("ClipRadius",typeof(CornerRadius),typeof(FrostWindow),new PropertyMetadata(new CornerRadius(),(d,_)=>
    {
        if(d is not Window window)return;
        window.SizeChanged-=SizeChanged;window.SizeChanged+=SizeChanged;
        window.DpiChanged-=DpiChanged;window.DpiChanged+=DpiChanged;
        ClipNative(window);
    }));
    private static readonly DependencyPropertyKey HasBackdropPropertyKey=DependencyProperty.RegisterAttachedReadOnly("HasBackdrop",typeof(bool),typeof(FrostWindow),new PropertyMetadata(false));
    public static readonly DependencyProperty HasBackdropProperty=HasBackdropPropertyKey.DependencyProperty;
    public static WindowMaterial GetMaterial(DependencyObject d)=>(WindowMaterial)d.GetValue(MaterialProperty);
    public static void SetMaterial(DependencyObject d,WindowMaterial v)=>d.SetValue(MaterialProperty,v);
    public static bool GetIsDark(DependencyObject d)=>(bool)d.GetValue(IsDarkProperty);
    public static void SetIsDark(DependencyObject d,bool v)=>d.SetValue(IsDarkProperty,v);
    public static AmbientState GetState(DependencyObject d)=>(AmbientState)d.GetValue(StateProperty);
    public static void SetState(DependencyObject d,AmbientState v)=>d.SetValue(StateProperty,v);
    public static bool GetHasBackdrop(DependencyObject d)=>(bool)d.GetValue(HasBackdropProperty);
    public static bool GetSquareCorners(DependencyObject d)=>(bool)d.GetValue(SquareCornersProperty);
    public static void SetSquareCorners(DependencyObject d,bool value)=>d.SetValue(SquareCornersProperty,value);
    public static CornerRadius GetClipRadius(DependencyObject d)=>(CornerRadius)d.GetValue(ClipRadiusProperty);
    public static void SetClipRadius(DependencyObject d,CornerRadius value)=>d.SetValue(ClipRadiusProperty,value);

    private static void MaterialChanged(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        if(d is not Window window)return;
        window.SourceInitialized-=SourceReady;window.SourceInitialized+=SourceReady;
        window.Loaded-=Loaded;window.Loaded+=Loaded;
        Apply(window);
    }
    private static void Loaded(object sender,RoutedEventArgs e)
    {
        var window=(Window)sender;
        PropertyChangedEventHandler handler=(_,change)=>
        {
            if(change.PropertyName==nameof(SystemParameters.HighContrast) && !window.Dispatcher.HasShutdownStarted)
                window.Dispatcher.BeginInvoke(()=>Apply(window));
        };
        SystemParameters.StaticPropertyChanged+=handler;
        window.Closed+=(_,_)=>SystemParameters.StaticPropertyChanged-=handler;
        window.Loaded-=Loaded;
    }
    private static void SourceReady(object? sender,EventArgs e)=>Apply((Window)sender!);
    private static void Apply(Window window)
    {
        if(window.AllowsTransparency)return;
        var handle=new WindowInteropHelper(window).Handle;
        if(handle==0)return;
        var chrome=WindowChrome.GetWindowChrome(window);
        if(chrome is null)
        {
            chrome=new WindowChrome {CaptionHeight=0,ResizeBorderThickness=new Thickness(window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip?6:0),GlassFrameThickness=new Thickness(-1),UseAeroCaptionButtons=false};
            WindowChrome.SetWindowChrome(window,chrome);
        }
        var supported=false;
        if(OperatingSystem.IsWindowsVersionAtLeast(10,0,22621))
        {
            var dark=GetIsDark(window)?1:0;
            _=DwmSetWindowAttribute(handle,20,ref dark,sizeof(int));
            var corners=GetSquareCorners(window)?1:2;_=DwmSetWindowAttribute(handle,33,ref corners,sizeof(int));
            var material=SystemParameters.HighContrast?1:(int)GetMaterial(window);
            supported=material>=2 && DwmSetWindowAttribute(handle,38,ref material,sizeof(int))==0;
            if(!supported){material=1;_=DwmSetWindowAttribute(handle,38,ref material,sizeof(int));}
        }
        window.SetValue(HasBackdropPropertyKey,supported);
        if(HwndSource.FromHwnd(handle)?.CompositionTarget is { } target)target.BackgroundColor=Colors.Transparent;
        ClipNative(window);
    }
    private static void SizeChanged(object sender,SizeChangedEventArgs e)=>ClipNative((Window)sender);
    private static void DpiChanged(object sender,DpiChangedEventArgs e)=>ClipNative((Window)sender,e.NewDpi);
    private static void ClipNative(Window window,DpiScale? scale=null)
    {
        var radius=GetClipRadius(window).TopLeft;var handle=new WindowInteropHelper(window).Handle;
        if(radius<=0 || handle==0 || window.ActualWidth<=0 || window.ActualHeight<=0)return;
        var dpi=scale??VisualTreeHelper.GetDpi(window);
        var region=CreateRoundRectRgn(0,0,(int)Math.Ceiling(window.ActualWidth*dpi.DpiScaleX)+1,(int)Math.Ceiling(window.ActualHeight*dpi.DpiScaleY)+1,(int)Math.Round(radius*2*dpi.DpiScaleX),(int)Math.Round(radius*2*dpi.DpiScaleY));
        if(region!=0 && SetWindowRgn(handle,region,true)==0)DeleteObject(region);
    }
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(nint window,int attribute,ref int value,int size);
    [DllImport("gdi32.dll")]private static extern nint CreateRoundRectRgn(int left,int top,int right,int bottom,int width,int height);
    [DllImport("user32.dll")]private static extern int SetWindowRgn(nint window,nint region,[MarshalAs(UnmanagedType.Bool)]bool redraw);
    [DllImport("gdi32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool DeleteObject(nint value);
}
