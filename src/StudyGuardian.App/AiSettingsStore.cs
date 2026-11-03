using System.ComponentModel;
using System.IO;
using System.Text.Json;
using StudyGuardian.Core;
using StudyGuardian.Monitor;

namespace StudyGuardian.App;

public sealed record AiPreferences(bool Enabled, string Model);

public sealed class AiSettingsStore
{
    private readonly string _path;
    private readonly ProtectedSecretStore _secret;
    public AiPreferences Current { get; private set; }
    public bool HasKey => _secret.Exists;
    public bool IsConfigured => Current.Enabled && HasKey;
    public string? LoadError { get; }

    public AiSettingsStore(string dataDirectory, AiOptions defaults)
    {
        _path = Path.Combine(dataDirectory, "settings.json");
        _secret = new(Path.Combine(dataDirectory, "api-key.dpapi"));
        Current = new(true, defaults.Model);
        try
        {
            if (File.Exists(_path))
            {
                var loaded = JsonSerializer.Deserialize<AiPreferences>(File.ReadAllText(_path));
                if (loaded is null || string.IsNullOrWhiteSpace(loaded.Model)) throw new JsonException();
                Current = loaded;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { LoadError = "设置无法读取，已使用默认值"; }
    }

    public string? ReadKey()
    {
        try { return _secret.Read(); }
        catch (Exception ex) when (ex is IOException or Win32Exception or UnauthorizedAccessException)
        { throw new AiUnavailableException("API Key 无法读取，请重新保存"); }
    }

    public void Save(AiPreferences preferences, string? newKey, bool clearKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferences.Model);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true }));
            if (!string.IsNullOrWhiteSpace(newKey)) _secret.Save(newKey.Trim());
            else if (clearKey) _secret.Save(null);
            File.Move(temporary, _path, overwrite: true);
            Current = preferences;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
