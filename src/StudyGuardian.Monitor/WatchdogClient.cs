using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
namespace StudyGuardian.Monitor;

public sealed class WatchdogClient:IDisposable
{
    private readonly string _application;private EventWaitHandle? _stop;
    public bool IsEnabled => _stop is not null;
    public bool IsStopping => _stop?.WaitOne(0)==true;
    public static string EventPrefix(string application)
    {
        var bytes=Encoding.UTF8.GetBytes(Path.GetFullPath(application).ToUpperInvariant());
        return @"Local\StudyGuardian.Stop-"+Convert.ToHexString(SHA256.HashData(bytes))+"-";
    }
    public static bool ValidEvent(string application,string name)
    {
        var prefix=EventPrefix(application);return name.StartsWith(prefix,StringComparison.Ordinal) && name.Length==prefix.Length+32 && name[prefix.Length..].All(Uri.IsHexDigit);
    }
    public WatchdogClient(string application,string? inheritedEvent=null)
    {
        _application=Path.GetFullPath(application);
        if(inheritedEvent is not null)
        {
            if(!ValidEvent(_application,inheritedEvent)) throw new ArgumentException("守护参数无效");
            _stop=EventWaitHandle.OpenExisting(inheritedEvent);
        }
    }
    public void SetEnabled(bool enabled)
    {
        if(!enabled) {Stop();return;}
        if(IsEnabled) return;
        var helper=Path.Combine(Path.GetDirectoryName(_application)!,"watchdog","StudyGuardian.Watchdog.exe");
        if(!File.Exists(helper)) throw new FileNotFoundException("缺少守护程序，请使用完整发布包");
        var name=EventPrefix(_application)+Guid.NewGuid().ToString("N");
        var stop=new EventWaitHandle(false,EventResetMode.ManualReset,name);
        try
        {
            using var current=Process.GetCurrentProcess();
            var start=new ProcessStartInfo(helper) {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(_application)!};
            start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            start.ArgumentList.Add(current.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));start.ArgumentList.Add(name);
            using var child=Process.Start(start) ?? throw new InvalidOperationException("无法启动守护程序");_stop=stop;
        }
        catch {stop.Dispose();throw;}
    }
    public void Stop() {if(_stop is not { } stop)return;_stop=null;stop.Set();stop.Dispose();}
    public void Dispose()=>Stop();
}
