using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace StudyGuardian.App.Controls;

public partial class WindowHeader : UserControl
{
    public WindowHeader()=>InitializeComponent();
    private void ThemeClick(object sender,RoutedEventArgs e)=>ThemeResources.Current.SetDark(!ThemeResources.Current.IsDark);
    private void CloseClick(object sender,RoutedEventArgs e)=>Window.GetWindow(this)?.Close();
    private void MinimizeClick(object sender,RoutedEventArgs e){if(Window.GetWindow(this) is { } window)window.WindowState=WindowState.Minimized;}
    private void DragHeader(object sender,MouseButtonEventArgs e)
    {
        for(var node=e.OriginalSource as DependencyObject;node is not null;node=VisualTreeHelper.GetParent(node))if(node is ButtonBase)return;
        if(Window.GetWindow(this) is not { } window || e.LeftButton!=MouseButtonState.Pressed)return;
        if(e.ClickCount==2 && window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip)
            window.WindowState=window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
        else window.DragMove();
    }
}
