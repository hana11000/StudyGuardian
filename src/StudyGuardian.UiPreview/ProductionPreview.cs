using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StudyGuardian.App;
using StudyGuardian.App.Controls;
using StudyGuardian.Core;
using StudyGuardian.Data;
using StudyGuardian.Monitor;

namespace StudyGuardian.UiPreview;

internal static class ProductionPreview
{
    private static readonly DateTimeOffset SampleStart=new(2026,11,3,9,0,0,TimeSpan.FromHours(8));
    public static void LoadTheme()=>Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("pack://application:,,,/StudyGuardian.App;component/Theme.xaml")});
    private static StudySessionSnapshot SampleState()=>new(true,false,false,new(12,34,"Notion.exe","第3章 增值税"),PauseReason.None,new(StudyVerdict.OnTask,DecisionSource.Rule),TimeSpan.FromSeconds(2292),.92);

    public static async Task RenderAsync(string directory)
    {
        Directory.CreateDirectory(directory);LoadTheme();
        var scratch=Path.Combine(directory,"synthetic-runtime");
        var root=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"config","defaults.json")))!;
        var json=new JsonSerializerOptions {PropertyNameCaseInsensitive=true};T Read<T>(string name)=>root[name]!.Deserialize<T>(json)!;
        using var watchdog=new WatchdogClient(Path.Combine(scratch,"StudyGuardian.App.exe"));
        var main=new MainWindow(Read<MonitoringOptions>("monitoring"),Read<SessionUiOptions>("session"),Read<AiOptions>("ai"),Read<InterventionOptions>("intervention"),Read<BrowserOptions>("browser"),Read<SessionToolsOptions>("sessionTools"),Read<DesktopOptions>("desktop"),watchdog,Path.Combine(scratch,"synthetic.db"));
        var setup=(SessionSetupControl)main.FindName("SetupControl");setup.IsEnabled=true;
        ((TextBox)setup.FindName("GoalBox")).Text="用 Notion 学习税法一第3章 增值税";
        setup.ApplyPolicy(new("增值税",[
            new("demo1",RuleKind.Allow,RuleMatchType.Process,"Notion.exe"),new("demo2",RuleKind.Allow,RuleMatchType.Domain,"dongao.com"),
            new("demo3",RuleKind.Supportive,RuleMatchType.Process,"CalculatorApp.exe"),new("demo4",RuleKind.Supportive,RuleMatchType.TitleRegex,"税法讲解"),
            new("demo5",RuleKind.Block,RuleMatchType.Domain,"douyin.com")],5));
        ((Button)main.FindName("StartButton")).IsEnabled=true;
        ((Button)main.FindName("SettingsButton")).IsEnabled=true;
        ((Button)main.FindName("DataFolderButton")).IsEnabled=true;
        ((Button)main.FindName("ReportButton")).IsEnabled=true;
        var controller=(SessionController)typeof(MainWindow).GetField("_controller",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!;
        var settings=new SettingsWindow(new AiSettingsStore(scratch,Read<AiOptions>("ai")),controller,new UserSettingsStore(scratch,Read<InterventionOptions>("intervention")),new AutostartRegistration(Path.Combine(scratch,"StudyGuardian.App.exe"),new InertStartupStore()));
        var session=new StoredStudySession("synthetic-report","税法一 · 第3章 增值税",SampleStart,SampleStart.AddMinutes(52),SessionStrictness.Standard,"completed",null,5);
        var reportData=new SessionReport(session,TimeSpan.FromMinutes(43),TimeSpan.FromMinutes(2),TimeSpan.FromMinutes(5),TimeSpan.FromMinutes(2),TimeSpan.Zero,4,1,
            [new(SampleStart,TimeSpan.FromMinutes(25),StudyVerdict.OnTask,"Notion"),new(SampleStart.AddMinutes(25),TimeSpan.FromMinutes(2),StudyVerdict.Supportive,"计算器"),new(SampleStart.AddMinutes(27),TimeSpan.FromMinutes(5),StudyVerdict.Distracted,"浏览器"),new(SampleStart.AddMinutes(32),TimeSpan.FromMinutes(18),StudyVerdict.OnTask,"Notion"),new(SampleStart.AddMinutes(50),TimeSpan.FromMinutes(2),StudyVerdict.Neutral,"休息")],
            [new("微信",TimeSpan.FromMinutes(3)),new("哔哩哔哩",TimeSpan.FromMinutes(2))]);
        var report=new ReportWindow([session],_=>Task.FromResult(reportData));
        var floating=new FloatingWindow(1);floating.Update(SampleState(),"增值税",aiStatus:"AI 已启用");
        var escalation=new EscalationSnapshot(InterventionLevel.Block,true,false,new(24,35,"chrome.exe","游戏实况"),TimeSpan.FromSeconds(90),TimeSpan.FromSeconds(380),TimeSpan.FromSeconds(380),TimeSpan.Zero,TimeSpan.FromSeconds(60),1,1);
        var tools=new SessionToolsSnapshot(2,TimeSpan.Zero,TimeSpan.Zero);
        var block=new InterventionWindow(InterventionSurface.Block,Read<InterventionOptions>("intervention"),false);
        var notice=new InterventionWindow(InterventionSurface.Notice,Read<InterventionOptions>("intervention"),false);
        var edge=new InterventionWindow(InterventionSurface.Frame,Read<InterventionOptions>("intervention"),false);
        block.Update("增值税",escalation,"Notion.exe",tools,true);
        notice.Update("增值税",escalation with {Level=InterventionLevel.Reminder,LockRemaining=TimeSpan.Zero},"Notion.exe",tools,true);
        try
        {
            foreach(var dark in new[]{false,true})
            {
                ThemeResources.Current.SetDark(dark);
                await Dispatcher.Yield(DispatcherPriority.Background);
                var prefix=dark?"dark":"light";
                await Save(main,Path.Combine(directory,prefix+"-start.png"),800,880);
                await Save(settings,Path.Combine(directory,prefix+"-settings.png"),720,840);
                await Save(report,Path.Combine(directory,prefix+"-report.png"),800,900);
                await Save(floating,Path.Combine(directory,prefix+"-floating.png"),260,44);
                await Save(block,Path.Combine(directory,prefix+"-block.png"),1024,720);
                await Save(notice,Path.Combine(directory,prefix+"-notice.png"),340,280);
            }
            ThemeResources.Current.SetDark(false);await Dispatcher.Yield(DispatcherPriority.Background);
            await Save(main,Path.Combine(directory,"light-start-compact.png"),620,720);
            ((FrameworkElement)floating.FindName("ExpandedPanel")).Visibility=Visibility.Visible;
            await Save(floating,Path.Combine(directory,"light-floating-expanded.png"),260,112);
            await Save(edge,Path.Combine(directory,"warning-edge.png"),1024,720);
            File.WriteAllText(Path.Combine(directory,"render-info.json"),JsonSerializer.Serialize(new {syntheticData=true,desktopCaptured=false,nativeWindowsCreated=new Window[]{main,settings,report,floating,block,notice,edge}.Any(w=>new WindowInteropHelper(w).Handle!=0)}));
        }
        finally
        {
            settings.Close();report.Close();floating.ClosePermanently();block.ClosePermanently();notice.ClosePermanently();edge.ClosePermanently();
            main.AllowSystemExit();main.Close();await Dispatcher.Yield(DispatcherPriority.Background);
        }
    }
    private static async Task Save(Window window,string path,int width,int height)
    {
        window.ApplyTemplate();
        await Dispatcher.Yield(DispatcherPriority.Background);
        var visual=(FrameworkElement)VisualTreeHelper.GetChild(window,0);
        visual.Measure(new Size(width,height));visual.Arrange(new Rect(0,0,width,height));visual.UpdateLayout();
        // The native window is never shown, so initialize only the decorative controls' lifecycle.
        var ambient=Descendants(visual).OfType<AmbientBackground>().ToArray();
        foreach(var light in ambient)light.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(path);encoder.Save(output);
        foreach(var light in ambient)light.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {yield return root;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(root,i)))yield return child;}
    public static void ShowFloatingSmoke(string output)
    {
        LoadTheme();Directory.CreateDirectory(output);
        var floating=new FloatingWindow(1){Title="Study Guardian · 悬浮窗预览"};floating.Update(SampleState(),"界面预览",aiStatus:"预览");
        Application.Current.MainWindow=floating;
        floating.EndRequested+=(_,_)=>floating.ClosePermanently();
        var timer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(150)};
        timer.Tick+=(_,_)=>{timer.Stop();File.WriteAllText(Path.Combine(output,"floating-smoke.json"),JsonSerializer.Serialize(new {backdrop=FrostWindow.GetHasBackdrop(floating),opacity=floating.Opacity,width=floating.ActualWidth,height=floating.ActualHeight}));floating.ClosePermanently();};
        floating.Closed+=(_,_)=>timer.Stop();floating.Show();timer.Start();
        File.WriteAllText(Path.Combine(output,"started.json"),JsonSerializer.Serialize(new {visible=floating.IsVisible,handle=new WindowInteropHelper(floating).Handle.ToInt64(),backdrop=FrostWindow.GetHasBackdrop(floating)}));
    }
    public static async Task VerifyFloatingInteractionAsync(string output)
    {
        LoadTheme();Directory.CreateDirectory(output);
        var floating=new FloatingWindow(1){Title="Study Guardian · 操作预览"};
        floating.Update(SampleState(),"界面预览",aiStatus:"预览");
        Application.Current.MainWindow=floating;
        try
        {
            floating.Show();await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            var work=DisplayMonitorProvider.GetMonitors().First(m=>m.IsPrimary).WorkArea;
            var dpi=VisualTreeHelper.GetDpi(floating);
            NativeWindowActions.SetPhysicalBounds(new WindowInteropHelper(floating).Handle,new(work.Left+work.Width-(int)(280*dpi.DpiScaleX),work.Top+work.Height-(int)(44*dpi.DpiScaleY),(int)(260*dpi.DpiScaleX),(int)(44*dpi.DpiScaleY)));
            floating.FocusControls();await Task.Delay(700);
            var surface=(FrameworkElement)floating.FindName("Surface");
            var entered=surface.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
            var pause=(Button)floating.FindName("PauseButton");
            var origin=floating.PointToScreen(new Point(0,0));dpi=VisualTreeHelper.GetDpi(floating);
            var bounds=new Rect(origin,new Size(floating.ActualWidth*dpi.DpiScaleX,floating.ActualHeight*dpi.DpiScaleY));
            var inside=new Rect(work.Left,work.Top,work.Width,work.Height).Contains(bounds);
            var keyboard=entered && pause.IsKeyboardFocused;
            File.WriteAllText(Path.Combine(output,"interaction.json"),JsonSerializer.Serialize(new {keyboardReachedPause=keyboard,expandedHeight=floating.ActualHeight,insideWorkArea=inside,syntheticData=true}));
            if(!keyboard || !inside || Math.Abs(floating.ActualHeight-112)>1)throw new InvalidOperationException("Floating keyboard/placement verification failed.");
        }
        finally {floating.ClosePermanently();}
    }
    private sealed class InertStartupStore : IStartupValueStore
    {public string? Read(string name)=>null;public void Write(string name,string command)=>throw new InvalidOperationException();public void Remove(string name)=>throw new InvalidOperationException();}
}
