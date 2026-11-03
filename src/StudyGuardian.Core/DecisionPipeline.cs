using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StudyGuardian.Core;

public sealed class DecisionPipeline(IClock clock, IStudyAi ai, IDecisionCache cache, AiOptions options, string model)
{
    private TimeSpan _retryAfter;
    private static readonly RuleDecision Fallback = new(StudyVerdict.Supportive, DecisionSource.Fallback, 0, "暂时无法判定");

    public async Task<RuleDecision> DecideAsync(DecisionRequest request, RuleDecision local, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (local.Verdict != StudyVerdict.Unknown) return local;
        var keys = BuildKeys(request);
        foreach (var scope in new[] { CacheScope.ExactUrl, CacheScope.Title, CacheScope.Domain, CacheScope.Process })
        {
            if (!keys.TryGetValue(scope, out var key)) continue;
            try
            {
                if (await cache.FindDecisionAsync(key, clock.UtcNow, cancellationToken) is { } hit && hit.ExpiresAt > clock.UtcNow)
                    return hit.Decision with { Source = DecisionSource.Cache };
            }
            catch (Exception ex) when (ex is DbException or IOException) { /* Cache failure must not stop the session. */ }
        }
        if (clock.Elapsed < _retryAfter) return Fallback;
        try
        {
            var result = await ai.ClassifyAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var verdict = result.Confidence < options.MinimumConfidence ? StudyVerdict.Uncertain : result.Verdict;
            var decision = new RuleDecision(verdict, DecisionSource.Llm, result.Confidence, result.Reason);
            var scope = keys.ContainsKey(result.CacheScope) ? result.CacheScope :
                keys.ContainsKey(CacheScope.ExactUrl) ? CacheScope.ExactUrl : CacheScope.Title;
            var ttl = Math.Clamp(result.CacheTtlMinutes, 0, options.CacheMaxTtlMinutes);
            if (ttl > 0)
            {
                try { await cache.SaveDecisionAsync(new(keys[scope], decision, clock.UtcNow.AddMinutes(ttl)), cancellationToken); }
                catch (Exception ex) when (ex is DbException or IOException) { }
            }
            return decision;
        }
        catch (AiUnavailableException)
        {
            _retryAfter = clock.Elapsed + TimeSpan.FromSeconds(options.FailureBackoffSeconds);
            return Fallback;
        }
    }

    private Dictionary<CacheScope, string> BuildKeys(DecisionRequest request)
    {
        var process = NormalizeProcess(request.Current.ProcessName);
        var uri = Uri.TryCreate(request.Current.Url, UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https" ? parsed : null;
        var domain = uri?.IdnHost.ToLowerInvariant().TrimEnd('.') ?? "";
        var policy = JsonSerializer.Serialize(new
        {
            version = "m3-v1", model, options.MinimumConfidence, goal = request.Goal.Trim(),
            rules = request.Rules.Select(r => new { r.Kind, r.MatchType, r.Pattern }).OrderBy(r => r.Kind).ThenBy(r => r.MatchType).ThenBy(r => r.Pattern, StringComparer.Ordinal)
        });
        var fingerprint = Hash(policy);
        string Key(CacheScope scope, string value) => Hash(JsonSerializer.Serialize(new { fingerprint, scope, process, value }));
        var title = string.Join(' ', request.Current.Title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Dictionary<CacheScope, string> keys = new() { [CacheScope.Title] = Key(CacheScope.Title, domain + "\n" + title) };
        if (uri is not null) keys[CacheScope.ExactUrl] = Key(CacheScope.ExactUrl, uri.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped));
        var mixed = options.MixedContentDomains.Any(d => domain == d || domain.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));
        if (uri is not null && !mixed) keys[CacheScope.Domain] = Key(CacheScope.Domain, domain);
        if (uri is null && !options.BrowserProcesses.Any(p => NormalizeProcess(p) == process))
            keys[CacheScope.Process] = Key(CacheScope.Process, process);
        return keys;
    }

    private static string NormalizeProcess(string value) => value.Trim().ToLowerInvariant().EndsWith(".exe", StringComparison.Ordinal)
        ? value.Trim().ToLowerInvariant()[..^4] : value.Trim().ToLowerInvariant();
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
