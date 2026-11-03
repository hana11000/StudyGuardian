using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StudyGuardian.Core;

namespace StudyGuardian.App;

internal sealed class AppealWindow : Window
{
    private readonly CancellationTokenSource _cancel=new();
    public AppealWindow(WindowSnapshot target,int maximumCharacters,int grantMinutes,Func<string,CancellationToken,Task<AppealDecision>> submit)
    {
        Title="申诉";Width=480;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Style=(Style)Application.Current.FindResource(typeof(Window));
        var panel=new StackPanel {Margin=new(24)};
        panel.Children.Add(new TextBlock {Text="这是学习需要",FontSize=21,FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,16)});
        panel.Children.Add(new TextBlock {Text=target.Title,TextTrimming=TextTrimming.CharacterEllipsis,Foreground=Brush("MutedBrush"),Margin=new(0,0,0,16)});
        var reason=new TextBox {MinHeight=92,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MaxLength=maximumCharacters};
        System.Windows.Automation.AutomationProperties.SetName(reason,"申诉理由");panel.Children.Add(reason);
        var status=new TextBlock {Foreground=Brush("DangerBrush"),TextWrapping=TextWrapping.Wrap,Margin=new(0,12,0,0)};panel.Children.Add(status);
        var button=new Button {Content="提交申诉",Style=(Style)Application.Current.FindResource("PrimaryButton"),Margin=new(0,24,0,0)};panel.Children.Add(button);
        var approved=false;
        button.Click+=async(_,_)=>
        {
            if(approved) {Close();return;}
            if(string.IsNullOrWhiteSpace(reason.Text)) {status.Text="请填写理由";return;}
            button.IsEnabled=false;reason.IsEnabled=false;status.Text="复核中…";status.Foreground=Brush("MutedBrush");
            try
            {
                var result=await submit(reason.Text.Trim(),_cancel.Token);
                if(_cancel.IsCancellationRequested) return;
                status.Text=result.Approved?$"已放行 {grantMinutes} 分钟":result.Reason;status.Foreground=Brush(result.Approved?"SuccessBrush":"DangerBrush");
                if(result.Approved) {approved=true;button.Content="完成";button.IsEnabled=true;return;}
            }
            catch(OperationCanceledException) when(_cancel.IsCancellationRequested) {return;}
            catch(AiUnavailableException e) {status.Text=e.Message;status.Foreground=Brush("DangerBrush");}
            catch(Exception) {status.Text="复核失败，请稍后重试";status.Foreground=Brush("DangerBrush");}
            button.IsEnabled=true;reason.IsEnabled=true;
        };
        Closed+=(_,_)=>_cancel.Cancel();Content=panel;Loaded+=(_,_)=>reason.Focus();
    }
    private static Brush Brush(string key)=>(Brush)Application.Current.FindResource(key);
}
