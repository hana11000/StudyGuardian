using StudyGuardian.Core;
namespace StudyGuardian.Monitor;
public interface IWatchedProcess : IDisposable {bool HasExited {get;}}
public interface IWatchdogHost
{
    IWatchedProcess? Attach(int processId,long startedUtcTicks,string path);
    IWatchedProcess Start(string path,string stopEvent);
    bool WaitForStop(int milliseconds);
}
public enum WatchdogExit {Stopped,IdentityMismatch,RestartLimit,LaunchFailed}
public static class WatchdogSupervisor
{
    public static WatchdogExit Run(IWatchdogHost host,IClock clock,DesktopOptions options,int processId,long startedUtcTicks,string path,string stopEvent)
    {
        options.Validate();var process=host.Attach(processId,startedUtcTicks,path);
        if(process is null) return WatchdogExit.IdentityMismatch;
        var budget=new RestartBudget(clock,options);
        try
        {
            while(true)
            {
                while(!process.HasExited) if(host.WaitForStop(options.WatchdogPollMilliseconds)) return WatchdogExit.Stopped;
                if(host.WaitForStop(options.RestartDelayMilliseconds)) return WatchdogExit.Stopped;
                if(!budget.TryRestart()) return WatchdogExit.RestartLimit;
                process.Dispose();
                try {process=host.Start(path,stopEvent);budget.Started();}
                catch(Exception) {return WatchdogExit.LaunchFailed;}
            }
        }
        finally {process.Dispose();}
    }
}
