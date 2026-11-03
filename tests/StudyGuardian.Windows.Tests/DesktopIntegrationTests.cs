using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;
using StudyGuardian.Monitor;
namespace StudyGuardian.Windows.Tests;
[TestClass]
public class DesktopIntegrationTests
{
    [TestMethod] public void AutostartQuotesPathsAndDoesNotRemoveAnotherCommand()
    {
        var store=new Values();var registration=new AutostartRegistration(@"C:\Synthetic App\StudyGuardian.App.exe",store);
        Assert.IsFalse(registration.IsEnabled);registration.SetEnabled(true);Assert.IsTrue(registration.IsEnabled);
        Assert.AreEqual("\"C:\\Synthetic App\\StudyGuardian.App.exe\"",store.Value);
        store.Value="another program";registration.SetEnabled(false);Assert.AreEqual("another program",store.Value);
    }
    [TestMethod] public void WatchdogRestartsCrashesButStopsAtRapidFailureLimit()
    {
        var host=new Host();var result=WatchdogSupervisor.Run(host,new Clock(),Options,1,1,"app","stop");
        Assert.AreEqual(WatchdogExit.RestartLimit,result);Assert.AreEqual(3,host.Starts);
    }
    [TestMethod] public void IntentionalStopDoesNotRestartAndIdentityMismatchIsRejected()
    {
        var host=new Host {Stop=true};Assert.AreEqual(WatchdogExit.Stopped,WatchdogSupervisor.Run(host,new Clock(),Options,1,1,"app","stop"));Assert.AreEqual(0,host.Starts);
        host=new Host {Mismatch=true};Assert.AreEqual(WatchdogExit.IdentityMismatch,WatchdogSupervisor.Run(host,new Clock(),Options,1,1,"app","stop"));Assert.AreEqual(0,host.Starts);
    }

    [TestMethod] public void NativeAttachChecksStartTimeAndFullExecutablePath()
    {
        using var stop=new EventWaitHandle(false,EventResetMode.ManualReset);
        using var current=System.Diagnostics.Process.GetCurrentProcess();var path=current.MainModule!.FileName;
        var host=new WindowsWatchdogHost(stop);var ticks=current.StartTime.ToUniversalTime().Ticks;
        using var valid=host.Attach(current.Id,ticks,path);Assert.IsNotNull(valid);
        Assert.IsNull(host.Attach(current.Id,ticks+1,path));Assert.IsNull(host.Attach(current.Id,ticks,path+".different"));
    }
    [TestMethod] public void InheritedStopEventIsBoundToExactInstallAndSignalsGracefulExit()
    {
        var application=Path.Combine(Path.GetTempPath(),"synthetic","StudyGuardian.App.exe");
        var name=WatchdogClient.EventPrefix(application)+Guid.NewGuid().ToString("N");
        using var signal=new EventWaitHandle(false,EventResetMode.ManualReset,name);
        Assert.IsTrue(WatchdogClient.ValidEvent(application,name));Assert.IsFalse(WatchdogClient.ValidEvent(application+"other",name));
        using var client=new WatchdogClient(application,name);Assert.IsTrue(client.IsEnabled);Assert.IsFalse(client.IsStopping);
        client.Stop();Assert.IsTrue(signal.WaitOne(0));
    }
    private static DesktopOptions Options=>new(){ExitPhrase="exit",ExitDelaySeconds=60,UiTickMilliseconds=250,WatchdogPollMilliseconds=250,RestartDelayMilliseconds=500,RapidFailureSeconds=60,MaximumRapidRestarts=3};
    private sealed class Clock:IClock {public TimeSpan Elapsed=>TimeSpan.Zero;public DateTimeOffset UtcNow=>DateTimeOffset.UnixEpoch;}
    private sealed class Values:IStartupValueStore {public string? Value;public string? Read(string name)=>Value;public void Write(string name,string command)=>Value=command;public void Remove(string name)=>Value=null;}
    private sealed class Process:IWatchedProcess {public bool HasExited=>true;public void Dispose() {}}
    private sealed class Host:IWatchdogHost
    {public int Starts;public bool Stop,Mismatch;public IWatchedProcess? Attach(int id,long started,string path)=>Mismatch?null:new Process();public IWatchedProcess Start(string path,string stopEvent){Starts++;return new Process();}public bool WaitForStop(int milliseconds)=>Stop;}
}
