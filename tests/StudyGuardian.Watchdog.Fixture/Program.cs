using System.Diagnostics;
using System.Text.Json;
using StudyGuardian.Monitor;
var root=AppContext.BaseDirectory;var path=Path.Combine(root,"StudyGuardian.App.exe");
var managed=args.Length==2 && args[0]=="--watchdog-managed";
var client=new WatchdogClient(path,managed?args[1]:null);
File.AppendAllText(Path.Combine(root,"launches.jsonl"),JsonSerializer.Serialize(new {pid=Environment.ProcessId,at=DateTimeOffset.UtcNow,managed})+Environment.NewLine);
var mode=File.ReadAllText(Path.Combine(root,"mode.txt")).Trim();
if(!managed) client.SetEnabled(true);
Thread.Sleep(1200);
if(mode=="stop" || (mode=="crash" && managed)) client.Stop();
File.AppendAllText(Path.Combine(root,"exits.jsonl"),JsonSerializer.Serialize(new {pid=Environment.ProcessId,at=DateTimeOffset.UtcNow})+Environment.NewLine);
// Unsignaled exit exercises the same OS process handle transition as a crash; no other process is killed.
return 17;
