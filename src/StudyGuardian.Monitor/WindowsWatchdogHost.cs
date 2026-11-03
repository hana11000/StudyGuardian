using System.Diagnostics;
using System.Runtime.InteropServices;
namespace StudyGuardian.Monitor;
public sealed class WindowsWatchdogHost(EventWaitHandle stop):IWatchdogHost
{
    private sealed class Watched(Process process):IWatchedProcess
    {public bool HasExited=>process.HasExited;public void Dispose()=>process.Dispose();}
    public IWatchedProcess? Attach(int processId,long startedUtcTicks,string path)
    {
        Process? process=null;
        try
        {
            process=Process.GetProcessById(processId);
            if(process.StartTime.ToUniversalTime().Ticks!=startedUtcTicks || !string.Equals(Path.GetFullPath(process.MainModule!.FileName),Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase))
            {process.Dispose();return null;}
            return new Watched(process);
        }
        catch(Exception) {process?.Dispose();return null;}
    }
    public IWatchedProcess Start(string path,string stopEvent)
    {
        var start=new ProcessStartInfo(path) {UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(path)!};
        start.ArgumentList.Add("--watchdog-managed");start.ArgumentList.Add(stopEvent);
        return new Watched(Process.Start(start) ?? throw new InvalidOperationException("无法重启程序"));
    }
    public bool WaitForStop(int milliseconds) => GetSystemMetrics(0x2000)!=0 || stop.WaitOne(milliseconds);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
}
