using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
namespace StudyGuardian.Monitor;
public interface IStartupValueStore {string? Read(string name);void Write(string name,string command);void Remove(string name);}
public sealed class RegistryStartupValueStore:IStartupValueStore
{
    private const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
    public string? Read(string name) {using var key=Registry.CurrentUser.OpenSubKey(KeyPath);return key?.GetValue(name) as string;}
    public void Write(string name,string command) {using var key=Registry.CurrentUser.CreateSubKey(KeyPath);key.SetValue(name,command,RegistryValueKind.String);}
    public void Remove(string name) {using var key=Registry.CurrentUser.OpenSubKey(KeyPath,true);key?.DeleteValue(name,false);}
}
public sealed class AutostartRegistration
{
    private readonly IStartupValueStore _store;private readonly string _command;
    public AutostartRegistration(string executable,IStartupValueStore store)
    {
        var path=Path.GetFullPath(executable);if(path.Contains('"')) throw new ArgumentException("程序路径无效");
        _store=store;_command="\""+path+"\"";Name="StudyGuardian-"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())));
    }
    public string Name {get;}
    public bool IsEnabled => string.Equals(_store.Read(Name),_command,StringComparison.OrdinalIgnoreCase);
    public void SetEnabled(bool enabled)
    {
        if(enabled)
        {
            if(_store.Read(Name) is { } existing && !string.Equals(existing,_command,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("自启项已被其他程序占用");
            if(!IsEnabled) _store.Write(Name,_command);
        }
        else if(IsEnabled) _store.Remove(Name);
    }
}
