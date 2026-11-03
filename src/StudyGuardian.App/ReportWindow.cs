using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using StudyGuardian.App.Controls;
using StudyGuardian.Core;

namespace StudyGuardian.App;

internal sealed class ReportWindow : Window
{
    private readonly StackPanel _body=new();
    private int _revision;
    public ReportWindow(IReadOnlyList<StoredStudySession> sessions,Func<StoredStudySession,Task<SessionReport>> load,string? selectedId=null)
    {
        Title="会话报告";Width=800;Height=840;MinWidth=560;MinHeight=480;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Style=(Style)Application.Current.FindResource(typeof(Window));
        var root=new Grid {Margin=new(32,16,32,32)};
        root.RowDefinitions.Add(new(){Height=GridLength.Auto});root.RowDefinitions.Add(new());
        var picker=new ComboBox {Margin=new(0,0,0,24)};
        foreach(var session in sessions)picker.Items.Add(new ComboBoxItem {Content=session.StartedAt.ToLocalTime().ToString("MM-dd HH:mm")+" · "+session.Goal,Tag=session});
        root.Children.Add(picker);
        var scroll=new ScrollViewer {Content=_body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,1);root.Children.Add(scroll);Content=root;
        picker.SelectionChanged+=async(_,_)=>
        {
            if(picker.SelectedItem is not ComboBoxItem {Tag:StoredStudySession session})return;
            var revision=++_revision;_body.Children.Clear();_body.Children.Add(UiVisuals.Text("读取中…"));
            try {var report=await load(session);if(revision==_revision)Render(report);}
            catch(Exception){if(revision==_revision){_body.Children.Clear();_body.Children.Add(UiVisuals.Text("报告读取失败"));}}
        };
        Closed+=(_,_)=>_revision++;
        if(sessions.Count>0)picker.SelectedIndex=Math.Max(0,sessions.ToList().FindIndex(s=>s.Id==selectedId));else _body.Children.Add(UiVisuals.Text("暂无会话"));
    }
    private void Render(SessionReport report)
    {
        _body.Children.Clear();
        var hero=new Grid {Margin=new(0,0,0,24)};
        hero.ColumnDefinitions.Add(new(){Width=new(192)});hero.ColumnDefinitions.Add(new());
        var gauge=new Grid {Width=160,Height=160,HorizontalAlignment=HorizontalAlignment.Left};
        gauge.Children.Add(new FocusRing {Progress=report.FocusRate??0});
        var center=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
        var percentage=UiVisuals.Text(report.FocusRate?.ToString("P0")??"—","M8.Timer");percentage.HorizontalAlignment=HorizontalAlignment.Center;center.Children.Add(percentage);
        var caption=UiVisuals.Text("专注率");caption.HorizontalAlignment=HorizontalAlignment.Center;center.Children.Add(caption);gauge.Children.Add(center);hero.Children.Add(gauge);
        var summary=new StackPanel {VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(summary,1);
        var goal=UiVisuals.Text(report.Session.Goal,"M8.Title");goal.TextWrapping=TextWrapping.Wrap;goal.MaxHeight=100;goal.TextTrimming=TextTrimming.CharacterEllipsis;summary.Children.Add(goal);
        var durations=UiVisuals.Text($"辅助 {Minutes(report.Supportive)} · 分心 {Minutes(report.Distracted)}");durations.Margin=new(0,16,0,0);summary.Children.Add(durations);hero.Children.Add(summary);_body.Children.Add(hero);
        var stats=new UniformGrid {Columns=2,Margin=new(-8,0,-8,24)};
        AddMetric(stats,"专注 · 分钟",report.Focused.TotalMinutes.ToString("0.#"));
        AddMetric(stats,"分心 · 次",report.DistractionEpisodes.ToString());
        AddMetric(stats,"拦截 · 次",report.Blocks.ToString());
        AddMetric(stats,"暂停 · 分钟",report.Paused.TotalMinutes.ToString("0.#"));
        _body.Children.Add(stats);
        var timeline=new Grid {Height=12};
        UiVisuals.SetCornerRadius(timeline,UiVisuals.Resource<CornerRadius>("M8.Radius.Timeline"));
        foreach(var slice in report.Timeline.Where(s=>s.Duration>TimeSpan.Zero))
        {
            var index=timeline.ColumnDefinitions.Count;timeline.ColumnDefinitions.Add(new(){Width=new(slice.Duration.Ticks,GridUnitType.Star)});
            var block=new Border {Background=ActivityRow.BrushFor(slice.Verdict),ToolTip=$"{slice.StartedAt.ToLocalTime():HH:mm:ss} · {ActivityRow.Countdown(slice.Duration)} · {ActivityRow.DisplayVerdict(slice.Verdict)}"};
            Grid.SetColumn(block,index);timeline.Children.Add(block);
        }
        var timelineBox=new Border {Child=timeline,CornerRadius=UiVisuals.Resource<CornerRadius>("M8.Radius.Timeline"),Margin=new(0,0,0,24)};
        timelineBox.SetResourceReference(Border.BackgroundProperty,"M8.Subtle");_body.Children.Add(timelineBox);
        var sources=new StackPanel();sources.Children.Add(UiVisuals.Text("分心来源","M8.Title"));
        if(report.Sources.Count==0)sources.Children.Add(UiVisuals.Text("—"));
        foreach(var source in report.Sources)
        {
            var row=new DockPanel {Margin=new(0,16,0,0)};
            var time=UiVisuals.Text(Minutes(source.Duration));DockPanel.SetDock(time,Dock.Right);row.Children.Add(time);
            var label=UiVisuals.Text(source.Name);label.SetResourceReference(TextBlock.ForegroundProperty,"M8.Text");label.TextTrimming=TextTrimming.CharacterEllipsis;row.Children.Add(label);sources.Children.Add(row);
        }
        _body.Children.Add(UiVisuals.Card(sources,true));
    }
    private static void AddMetric(Panel grid,string label,string value)
    {
        var body=new StackPanel();body.Children.Add(UiVisuals.Text(value,"M8.Timer"));
        var caption=UiVisuals.Text(label);caption.Margin=new(0,8,0,0);body.Children.Add(caption);
        var card=UiVisuals.Card(body,true);card.Margin=new(8);grid.Children.Add(card);
    }
    private static string Minutes(TimeSpan value)=>value.TotalMinutes>=1?$"{value.TotalMinutes:0.#} 分钟":$"{value.TotalSeconds:0} 秒";
}
