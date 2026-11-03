using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.App;
using StudyGuardian.Core;
using StudyGuardian.Data;
using StudyGuardian.Monitor;
namespace StudyGuardian.Windows.Tests;
[TestClass]
public class ExitWorkflowTests
{
    private static readonly Lazy<Dispatcher> Ui = new(() =>
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/StudyGuardian.App;component/Theme.xaml") });
                ready.SetResult(dispatcher); Dispatcher.Run();
            }
            catch (Exception ex) { ready.TrySetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return ready.Task.GetAwaiter().GetResult();
    });
    private static Task RunUi(Func<Task> body) => Ui.Value.InvokeAsync(body).Task.Unwrap();

    [TestMethod] public Task RepeatedExitDuringConfirmationDoesNotReenterWpfClosing() => RunUi(async () =>
    {
        // Construct an unshown window: no foreground interaction, hooks, tray or actual dialog.
        var root=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"config","defaults.json")))!;
        var json=new JsonSerializerOptions {PropertyNameCaseInsensitive=true};T Read<T>(string name)=>root[name]!.Deserialize<T>(json)!;
        var directory=Path.Combine(Path.GetTempPath(),"StudyGuardian.Windows.Tests",Guid.NewGuid().ToString("N"));
        try
        {
            var intervention=Read<InterventionOptions>("intervention");var preferences=new UserSettingsStore(directory,intervention);
            preferences.Save(preferences.Current with {Desktop=new(true,false)});
            using var watchdog=new WatchdogClient(Path.Combine(directory,"StudyGuardian.App.exe"));
            MainWindow? window=null;var closingReturned=false;var inline=false;Exception? reentry=null;var calls=0;
            Task<bool> Confirm()
            {
                calls++;inline=!closingReturned;
                try {window!.Close();}catch(InvalidOperationException e){reentry=e;}
                return Task.FromResult(false);
            }
            window=new(Read<MonitoringOptions>("monitoring"),Read<SessionUiOptions>("session"),Read<AiOptions>("ai"),intervention,
                Read<BrowserOptions>("browser"),Read<SessionToolsOptions>("sessionTools"),Read<DesktopOptions>("desktop"),watchdog,Path.Combine(directory,"test.db"),Confirm);
            window.Close();closingReturned=true;
            await Dispatcher.Yield(DispatcherPriority.Background);
            Assert.IsNull(reentry,"Repeated tray Exit must not throw inside WPF Closing");
            Assert.IsFalse(inline,"Confirmation must start after the original Closing callback returns");Assert.AreEqual(1,calls);
            window.AllowSystemExit();window.Close();await Dispatcher.Yield(DispatcherPriority.Background);
        }
        finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
    });

    [TestMethod] public Task FloatingAiStatusSurvivesRoutineMainWindowRefresh() => RunUi(async () =>
    {
        var root=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"config","defaults.json")))!;
        var json=new JsonSerializerOptions {PropertyNameCaseInsensitive=true};T Read<T>(string name)=>root[name]!.Deserialize<T>(json)!;
        var directory=Path.Combine(Path.GetTempPath(),"StudyGuardian.Windows.Tests",Guid.NewGuid().ToString("N"));
        MainWindow? window=null; StudySession? session=null;
        try
        {
            var ai=Read<AiOptions>("ai");new AiSettingsStore(directory,ai).Save(new(true,"synthetic-model"),"synthetic-key",false);
            using var watchdog=new WatchdogClient(Path.Combine(directory,"StudyGuardian.App.exe"));
            window=new(Read<MonitoringOptions>("monitoring"),Read<SessionUiOptions>("session"),ai,Read<InterventionOptions>("intervention"),
                Read<BrowserOptions>("browser"),Read<SessionToolsOptions>("sessionTools"),Read<DesktopOptions>("desktop"),watchdog,Path.Combine(directory,"test.db"));
            // Seed only the pure session; never show a window or start foreground hooks.
            const BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
            var controller=(SessionController)typeof(MainWindow).GetField("_controller",fields)!.GetValue(window)!;
            var options=new StudySessionOptions("合成学习",TimeSpan.FromMinutes(50),SessionStrictness.Standard,[]);
            typeof(SessionController).GetProperty("Options")!.SetValue(controller,options);
            session=(StudySession)typeof(SessionController).GetField("_session",fields)!.GetValue(controller)!;
            var target=new WindowSnapshot(1,42,"notepad.exe","税法学习");session.Start("synthetic",options,target,TimeSpan.Zero);
            var floating=new FloatingWindow(.94);
            typeof(MainWindow).GetField("_floating",fields)!.SetValue(window,floating);
            var refresh=typeof(MainWindow).GetMethod("UpdateState",fields)!;
            refresh.Invoke(window,null);
            var status=(TextBlock)floating.FindName("AiText");
            Assert.AreEqual(controller.AiStatus,status.Text);
            StringAssert.Contains(status.Text,"等待判断");
            session.SetManualPause(true,target,TimeSpan.Zero);refresh.Invoke(window,null);
            Assert.AreEqual("AI 暂停",status.Text);
        }
        finally
        {
            session?.Stop();window?.AllowSystemExit();window?.Close();await Dispatcher.Yield(DispatcherPriority.Background);
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
    });
}
