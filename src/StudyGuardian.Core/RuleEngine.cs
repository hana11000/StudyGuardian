using System.Globalization;
using System.Text.RegularExpressions;

namespace StudyGuardian.Core;

public sealed class RuleEngine
{
    private sealed record CompiledRule(StudyRule Rule, string Pattern, Regex? Regex);
    private readonly CompiledRule[] _rules;
    private readonly HashSet<string> _protectedProcesses;
    private readonly int _ownProcessId;

    public RuleEngine(IEnumerable<StudyRule> rules, IEnumerable<string> protectedProcesses, int ownProcessId, TimeSpan regexTimeout)
    {
        if (regexTimeout <= TimeSpan.Zero || regexTimeout.TotalMilliseconds > int.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(regexTimeout));
        _ownProcessId = ownProcessId;
        _protectedProcesses = new(protectedProcesses.Select(NormalizeProcess), StringComparer.OrdinalIgnoreCase);
        _rules = rules.Select(rule =>
        {
            if (Validate(rule) is { } error) throw new ArgumentException(error, nameof(rules));
            var pattern = rule.MatchType switch
            {
                RuleMatchType.Process => NormalizeProcess(rule.Pattern),
                RuleMatchType.Domain => NormalizeDomain(rule.Pattern)!,
                RuleMatchType.Url => NormalizeUrl(ParseWebUrl(rule.Pattern)!),
                _ => rule.Pattern
            };
            return new CompiledRule(rule, pattern, rule.MatchType == RuleMatchType.TitleRegex
                ? new Regex(rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, regexTimeout) : null);
        }).OrderByDescending(r => r.Rule.MatchType switch
        {
            RuleMatchType.Url => 4, RuleMatchType.TitleRegex => 3, RuleMatchType.Domain => 2, _ => 1
        }).ThenByDescending(r => r.Rule.Kind switch { RuleKind.Block => 3, RuleKind.Allow => 2, _ => 1 }).ToArray();
    }

    public RuleDecision Evaluate(WindowSnapshot window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!window.IsAccessible || window.Handle == 0 || string.IsNullOrWhiteSpace(window.ProcessName) ||
            window.ProcessId == _ownProcessId || _protectedProcesses.Contains(NormalizeProcess(window.ProcessName)))
            return RuleDecision.Neutral;

        var url = ParseWebUrl(window.Url);
        var host = url?.IdnHost.TrimEnd('.');
        try
        {
            foreach (var entry in _rules)
            {
                var match = entry.Rule.MatchType switch
                {
                    RuleMatchType.Process => string.Equals(NormalizeProcess(window.ProcessName), entry.Pattern, StringComparison.OrdinalIgnoreCase),
                    RuleMatchType.Domain => host is not null && (host.Equals(entry.Pattern, StringComparison.OrdinalIgnoreCase) ||
                        host.EndsWith("." + entry.Pattern, StringComparison.OrdinalIgnoreCase)),
                    RuleMatchType.Url => url is not null && NormalizeUrl(url).Equals(entry.Pattern, StringComparison.Ordinal),
                    RuleMatchType.TitleRegex => entry.Regex!.IsMatch(window.Title),
                    _ => false
                };
                if (match) return new(entry.Rule.Kind switch
                {
                    RuleKind.Allow => StudyVerdict.OnTask, RuleKind.Supportive => StudyVerdict.Supportive, _ => StudyVerdict.Distracted
                }, DecisionSource.Rule);
            }
        }
        catch (RegexMatchTimeoutException) { return RuleDecision.Unknown; }
        return RuleDecision.Unknown;
    }

    public static string? Validate(StudyRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Pattern)) return "请输入规则";
        if (!Enum.IsDefined(rule.Kind) || !Enum.IsDefined(rule.MatchType)) return "规则类型无效";
        switch (rule.MatchType)
        {
            case RuleMatchType.Process:
                return NormalizeProcess(rule.Pattern).Length == 0 || rule.Pattern.IndexOfAny(['/', '\\', ':']) >= 0
                    ? "请输入进程名，如 Notion.exe" : null;
            case RuleMatchType.Domain:
                return NormalizeDomain(rule.Pattern) is null ? "请输入域名，如 notion.so" : null;
            case RuleMatchType.Url:
                return ParseWebUrl(rule.Pattern) is null ? "请输入完整的 http 或 https 网址" : null;
            case RuleMatchType.TitleRegex:
                try { _ = new Regex(rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant); }
                catch (ArgumentException) { return "正则表达式无效"; }
                break;
        }
        return null;
    }

    private static string NormalizeProcess(string value)
    {
        value = value.Trim();
        return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value[..^4] : value;
    }

    private static string? NormalizeDomain(string value)
    {
        try
        {
            var host = new IdnMapping().GetAscii(value.Trim().TrimEnd('.')).ToLowerInvariant();
            return Uri.CheckHostName(host) == UriHostNameType.Unknown ? null : host;
        }
        catch (ArgumentException) { return null; }
    }

    private static Uri? ParseWebUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" && uri.Host.Length > 0 ? uri : null;

    private static string NormalizeUrl(Uri uri) => uri.Scheme + "://" + uri.IdnHost.TrimEnd('.').ToLowerInvariant() +
        (uri.IsDefaultPort ? "" : ":" + uri.Port.ToString(CultureInfo.InvariantCulture)) + uri.AbsolutePath + uri.Query;
}
