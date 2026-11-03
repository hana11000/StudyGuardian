using System.Globalization;
using System.Text.Json;
using StudyGuardian.Core;
using StudyGuardian.Monitor;
try
{
    if(args.Length!=3 || !int.TryParse(args[0],out var pid) || !long.TryParse(args[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out var started)) return 2;
    var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,".."));var application=Path.Combine(root,"StudyGuardian.App.exe");
    if(!WatchdogClient.ValidEvent(application,args[2]) || !File.Exists(application)) return 2;
    using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"config","defaults.json")));
    var options=document.RootElement.GetProperty("desktop").Deserialize<DesktopOptions>(new JsonSerializerOptions {PropertyNameCaseInsensitive=true})!;options.Validate();
    using var stop=EventWaitHandle.OpenExisting(args[2]);
    return (int)WatchdogSupervisor.Run(new WindowsWatchdogHost(stop),new SystemClock(),options,pid,started,application,args[2]);
}
catch {return 2;}
