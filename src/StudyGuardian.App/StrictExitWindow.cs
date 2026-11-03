using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StudyGuardian.Core;
using StudyGuardian.App.Controls;
namespace StudyGuardian.App;
public sealed class StrictExitWindow:Window
{
    public StrictExitWindow(DesktopOptions options)
    {
        Style=(Style)Application.Current.FindResource(typeof(Window));
        Title="结束专注";Width=460;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var gate=new ExitGate(new SystemClock(),options);var panel=new StackPanel {Margin=new Thickness(24)};Content=UiVisuals.Card(panel);
        panel.Children.Add(new TextBlock {Text=options.ExitPhrase,FontSize=20,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16)});
        var input=new TextBox {Margin=new Thickness(0,0,0,16)};System.Windows.Automation.AutomationProperties.SetName(input,"退出确认文字");panel.Children.Add(input);
        var row=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};panel.Children.Add(row);
        var cancel=new Button {Content="继续专注",IsCancel=true,Margin=new Thickness(0,0,12,0)};cancel.Click+=(_,_)=>DialogResult=false;row.Children.Add(cancel);
        var quit=new Button {IsEnabled=false};quit.Click+=(_,_)=>{if(gate.CanExit) DialogResult=true;};row.Children.Add(quit);
        void Refresh(){quit.IsEnabled=gate.CanExit;quit.Content=gate.CanExit?"退出":"退出 · "+Math.Ceiling(gate.Remaining.TotalSeconds)+" 秒";}
        input.TextChanged+=(_,_)=>{gate.SetText(input.Text);Refresh();};
        var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(options.UiTickMilliseconds)};timer.Tick+=(_,_)=>Refresh();
        Loaded+=(_,_)=>{input.Focus();timer.Start();};Closed+=(_,_)=>timer.Stop();Refresh();
    }
}
