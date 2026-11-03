namespace StudyGuardian.Core;

public sealed class BrowserOptions
{
    public required int Port { get; init; }
    public required int TokenBytes { get; init; }
    public required int MaximumHeaderBytes { get; init; }
    public required int MaximumBodyBytes { get; init; }
    public required int MaximumConnections { get; init; }
    public required int RequestTimeoutMilliseconds { get; init; }
    public required int PollMilliseconds { get; init; }
    public required int HeartbeatSeconds { get; init; }
    public required int FreshnessSeconds { get; init; }
    public required int CommandTtlSeconds { get; init; }
    public required int MaximumClients { get; init; }
    public required int MaximumUrlCharacters { get; init; }
    public required int MaximumTitleCharacters { get; init; }
    public required string[] Processes { get; init; }
    public required string[] TitleSuffixes { get; init; }
    public void Validate()
    {
        if (Port is < 1024 or > 65535 || TokenBytes < 32 || MaximumHeaderBytes <= 0 || MaximumBodyBytes <= 0 ||
            MaximumConnections <= 0 || RequestTimeoutMilliseconds <= 0 || PollMilliseconds <= 0 || HeartbeatSeconds < 30 ||
            FreshnessSeconds <= 0 || CommandTtlSeconds <= 0 || MaximumClients <= 0 || MaximumUrlCharacters <= 0 ||
            MaximumTitleCharacters <= 0 || Processes is not { Length: > 0 } || TitleSuffixes is not { Length: > 0 } ||
            Processes.Any(string.IsNullOrWhiteSpace) || TitleSuffixes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("浏览器配置无效");
    }
}

public sealed record BrowserActivity(string ClientId, long Sequence, string Process, int WindowId, int TabId,
    string Title, string Url, bool Focused);
public sealed record BrowserCommand(string Action = "none", int? TabId = null, string? Url = null, string? Goal = null);
public sealed record BrowserStatus(bool Monitoring);
