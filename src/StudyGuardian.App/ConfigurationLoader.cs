using System.IO;
using System.Text.Json;
using StudyGuardian.Core;

namespace StudyGuardian.App;

public sealed class MonitoringOptions
{
    public required int AwayAfterSeconds { get; init; }
    public required int IdleSampleMilliseconds { get; init; }
    public required int CheckpointSeconds { get; init; }
    public required int RecentEventLimit { get; init; }
    public required int MaximumTitleCharacters { get; init; }
    public required string DatabasePath { get; init; }
    public required string[] ProtectedProcesses { get; init; }
}

public sealed class SessionUiOptions
{
    public required int[] PresetMinutes { get; init; }
    public required int DefaultDurationMinutes { get; init; }
    public required int MinimumDurationMinutes { get; init; }
    public required int MaximumDurationMinutes { get; init; }
    public required string DefaultStrictness { get; init; }
    public required double DecisionDwellSeconds { get; init; }
    public required int RegexTimeoutMilliseconds { get; init; }
    public required double FloatingOpacity { get; init; }
}

internal static class ConfigurationLoader
{
    private sealed record Configuration(MonitoringOptions Monitoring, SessionUiOptions Session, AiOptions Ai, InterventionOptions Intervention);

    public static (MonitoringOptions Options, SessionUiOptions Session, AiOptions Ai, InterventionOptions Intervention, string DatabasePath) Load()
    {
        var root = AppContext.BaseDirectory;
        var path = Path.Combine(root, "config", "defaults.json");
        var config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var options = config?.Monitoring ?? throw new InvalidDataException("config/defaults.json 缺少 monitoring 配置。");
        var session = config?.Session ?? throw new InvalidDataException("config/defaults.json 缺少 session 配置。");
        var ai = config?.Ai ?? throw new InvalidDataException("config/defaults.json 缺少 ai 配置。");
        var intervention = config?.Intervention ?? throw new InvalidDataException("config/defaults.json 缺少 intervention 配置。");
        intervention.Validate();
        if (options.AwayAfterSeconds <= 0 || options.IdleSampleMilliseconds <= 0 || options.CheckpointSeconds <= 0 ||
            options.RecentEventLimit <= 0 || options.MaximumTitleCharacters <= 0 || options.MaximumTitleCharacters == int.MaxValue ||
            options.ProtectedProcesses is null || options.ProtectedProcesses.Length == 0 || options.ProtectedProcesses.Any(string.IsNullOrWhiteSpace) ||
            string.IsNullOrWhiteSpace(options.DatabasePath) || Path.IsPathRooted(options.DatabasePath))
            throw new InvalidDataException("配置中的时长、数量必须为正数，进程名单不能为空，数据库路径必须为相对路径。");
        var databasePath = Path.GetFullPath(Path.Combine(root, options.DatabasePath));
        if (!databasePath.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("数据库必须位于程序目录内。");
        if (session.MinimumDurationMinutes <= 0 || session.MaximumDurationMinutes < session.MinimumDurationMinutes ||
            session.DefaultDurationMinutes < session.MinimumDurationMinutes || session.DefaultDurationMinutes > session.MaximumDurationMinutes ||
            session.PresetMinutes is not { Length: 3 } || session.PresetMinutes.Distinct().Count() != 3 ||
            session.PresetMinutes.Any(m => m < session.MinimumDurationMinutes || m > session.MaximumDurationMinutes) ||
            !Enum.TryParse<SessionStrictness>(session.DefaultStrictness, out var strictness) || !Enum.IsDefined(strictness) ||
            !double.IsFinite(session.DecisionDwellSeconds) || session.DecisionDwellSeconds < 0 ||
            session.RegexTimeoutMilliseconds <= 0 || session.RegexTimeoutMilliseconds == int.MaxValue ||
            !double.IsFinite(session.FloatingOpacity) || session.FloatingOpacity <= 0 || session.FloatingOpacity > 1)
            throw new InvalidDataException("会话配置中的时长、严格度或透明度无效。");
        if (string.IsNullOrWhiteSpace(ai.Model) || !double.IsFinite(ai.RequestTimeoutSeconds) || ai.RequestTimeoutSeconds <= 0 ||
            ai.CacheMaxTtlMinutes <= 0 || ai.FailureBackoffSeconds <= 0 || !double.IsFinite(ai.MinimumConfidence) || ai.MinimumConfidence < 0 || ai.MinimumConfidence > 1 ||
            ai.MaximumResponseBytes <= 0 || ai.MaximumOutputTokens <= 0 || ai.MaximumRules <= 0 || ai.MaximumGoalCharacters <= 0 ||
            ai.MaximumPatternCharacters <= 0 || ai.MaximumReasonCharacters <= 0 || ai.MaximumSupportiveMinutes <= 0 ||
            ai.DefaultSupportiveMinutes <= 0 || ai.DefaultSupportiveMinutes > ai.MaximumSupportiveMinutes || ai.RecentActivityLimit <= 0 ||
            ai.BrowserProcesses is not { Length: > 0 } || ai.MixedContentDomains is not { Length: > 0 } ||
            ai.BrowserProcesses.Any(string.IsNullOrWhiteSpace) || ai.MixedContentDomains.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("AI 配置中的模型、阈值或数量无效。");
        return (options, session, ai, intervention, databasePath);
    }
}
