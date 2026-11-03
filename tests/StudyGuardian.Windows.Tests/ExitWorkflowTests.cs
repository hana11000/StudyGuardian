using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.App;
using StudyGuardian.Core;
using StudyGuardian.Data;
using StudyGuardian.Monitor;
namespace StudyGuardian.Windows.Tests;
[TestClass]
public class ExitWorkflowTests
{
    [TestMethod] public Task RepeatedExitDuringConfirmationDoesNotReenterWpfClosing() => SessionControllerTests.RunDispatcher(async dispatcher=>
    {
        // Construct an unshown window: no foreground interaction, hooks, tray or actual dialog.
        var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        application.Resources.MergedDictionaries.Add(new ResourceDictionary {Source=new Uri("pack://application:,,,/StudyGuardian.App;component/Theme.xaml")});
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
}
