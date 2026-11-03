using System.Text.Json;
using System.Text.Json.Serialization;
using StudyGuardian.Core;

namespace StudyGuardian.Data;

public sealed class UserSettingsStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web)
    { WriteIndented=true,Converters={new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)} };
    public UserPreferences Current {get;private set;}
    public string? LoadError {get;}
    public UserSettingsStore(string dataDirectory,InterventionOptions defaults)
    {
        _path=Path.GetFullPath(Path.Combine(dataDirectory,"preferences.json"));Current=new(defaults,[],new());
        try
        {
            if(!File.Exists(_path)) return;
            var loaded=JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(_path),Json) ?? throw new JsonException();
            loaded.Validate();Current=loaded;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { LoadError="设置无法读取，已使用默认值"; }
    }
    public void Save(UserPreferences preferences)
    {
        preferences.Validate();Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary=_path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {File.WriteAllText(temporary,JsonSerializer.Serialize(preferences,Json));File.Move(temporary,_path,true);Current=preferences with {GlobalRules=preferences.GlobalRules.ToArray()};}
        finally {if(File.Exists(temporary)) File.Delete(temporary);}
    }
}
