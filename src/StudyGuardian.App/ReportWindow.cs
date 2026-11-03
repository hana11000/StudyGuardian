using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StudyGuardian.Core;

namespace StudyGuardian.App;

internal sealed class ReportWindow : Window
{
    private readonly StackPanel _body=new();
    private int _revision;
    public ReportWindow(IReadOnlyList<StoredStudySession> sessions,Func<StoredStudySession,Task<SessionReport>> load,string? selectedId=null)
    {
        Title="会话报告";Width=720;Height=620;MinWidth=560;MinHeight=400;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Style=(Style)Application.Current.FindResource(typeof(Window));var root=new Grid {Margin=new(24)};
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new());
        var picker=new ComboBox {Margin=new(0,0,0,24)};
        foreach(var session in sessions) picker.Items.Add(new ComboBoxItem {Content=session.StartedAt.ToLocalTime().ToString("MM-dd HH:mm")+" · "+session.Goal,Tag=session});
        root.Children.Add(picker);var scroll=new ScrollViewer {Content=_body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,1);root.Children.Add(scroll);Content=root;
        picker.SelectionChanged+=async(_,_)=>
        {
            if(picker.SelectedItem is not ComboBoxItem {Tag:StoredStudySession session}) return;
            var revision=++_revision;_body.Children.Clear();_body.Children.Add(Text("读取中…",14,"MutedBrush"));
            try {var report=await load(session);if(revision==_revision) Render(report);}
            catch(Exception) {if(revision==_revision) {_body.Children.Clear();_body.Children.Add(Text("报告读取失败",14,"DangerBrush"));}}
        };
        Closed+=(_,_)=>_revision++;
        if(sessions.Count>0) picker.SelectedIndex=Math.Max(0,sessions.ToList().FindIndex(s=>s.Id==selectedId));else _body.Children.Add(Text("暂无会话",16,"MutedBrush"));
    }
    private void Render(SessionReport report)
    {
        _body.Children.Clear();var goal=Text(report.Session.Goal,23);goal.TextWrapping=TextWrapping.Wrap;goal.Margin=new(0,0,0,24);_body.Children.Add(goal);
        var stats=new UniformGridPanel();
        stats.Add("专注",Minutes(report.Focused),"SuccessBrush");stats.Add("专注率",report.FocusRate?.ToString("P0")??"—","TextBrush");stats.Add("分心",$"{report.DistractionEpisodes} 次","AmberBrush");stats.Add("拦截",$"{report.Blocks} 次","DangerBrush");_body.Children.Add(stats.Panel);
        var durations=Text($"辅助 {Minutes(report.Supportive)}  ·  分心 {Minutes(report.Distracted)}  ·  暂停 {Minutes(report.Paused)}",13,"MutedBrush");durations.Margin=new(0,16,0,24);_body.Children.Add(durations);
        var timeline=new Grid {Height=24,Margin=new(0,0,0,24)};
        foreach(var slice in report.Timeline)
        {
            var index=timeline.ColumnDefinitions.Count;timeline.ColumnDefinitions.Add(new(){Width=new(slice.Duration.Ticks,GridUnitType.Star)});
            var block=new Border {Background=ActivityRow.BrushFor(slice.Verdict),ToolTip=$"{slice.StartedAt.ToLocalTime():HH:mm:ss} · {ActivityRow.Countdown(slice.Duration)} · {ActivityRow.DisplayVerdict(slice.Verdict)}"};Grid.SetColumn(block,index);timeline.Children.Add(block);
        }
        _body.Children.Add(timeline);
        _body.Children.Add(Text("分心来源",17));
        if(report.Sources.Count==0) {_body.Children.Add(Text("—",16,"MutedBrush"));return;}
        foreach(var source in report.Sources)
        {var row=new DockPanel {Margin=new(0,12,0,0)};var time=Text(Minutes(source.Duration),14,"MutedBrush");DockPanel.SetDock(time,Dock.Right);row.Children.Add(time);row.Children.Add(Text(source.Name,14));_body.Children.Add(row);}
    }
    private static string Minutes(TimeSpan value)=>value.TotalMinutes>=1?$"{value.TotalMinutes:0.#} 分钟":$"{value.TotalSeconds:0} 秒";
    private static TextBlock Text(string text,double size,string color="TextBrush")=>new(){Text=text,FontSize=size,Foreground=(Brush)Application.Current.FindResource(color)};
    private sealed class UniformGridPanel
    {
        public System.Windows.Controls.Primitives.UniformGrid Panel {get;}=new(){Columns=4};
        public void Add(string label,string value,string color)
        {var stack=new StackPanel();stack.Children.Add(Text(label,12,"MutedBrush"));var text=Text(value,27,color);text.FontFamily=new("Consolas");text.Margin=new(0,8,0,0);stack.Children.Add(text);Panel.Children.Add(stack);}
    }
}
