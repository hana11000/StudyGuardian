using System.Text.RegularExpressions;

namespace StudyGuardian.Core;

public sealed record RuleCandidate(string Label, string Detail, RuleMatchType MatchType, string Pattern)
{
    public string TypeLabel => MatchType switch
    {
        RuleMatchType.Process => "应用", RuleMatchType.Domain => "网站", RuleMatchType.Url => "网址", _ => "窗口标题"
    };
    public StudyRule ToRule(RuleKind kind) => new(Guid.NewGuid().ToString("N"), kind, MatchType, Pattern);

    public static IReadOnlyList<RuleCandidate> FromWindows(IEnumerable<WindowSnapshot> windows,
        IEnumerable<string> protectedProcesses, IEnumerable<string> browserProcesses, int ownProcessId)
    {
        var excluded = new HashSet<string>(protectedProcesses.Select(Normalize), StringComparer.OrdinalIgnoreCase);
        var browsers = new HashSet<string>(browserProcesses.Select(Normalize), StringComparer.OrdinalIgnoreCase);
        var choices = new List<RuleCandidate>();
        foreach (var window in windows)
        {
            var process = Normalize(window.ProcessName);
            if (!window.IsAccessible || window.Handle == 0 || window.ProcessId == ownProcessId ||
                process.Length == 0 || excluded.Contains(process) || string.IsNullOrWhiteSpace(window.Title)) continue;
            if (!browsers.Contains(process))
                choices.Add(new(process, window.Title, RuleMatchType.Process, window.ProcessName));
            else
            {
                choices.Add(new(window.Title, process, RuleMatchType.TitleRegex, "\\A" + Regex.Escape(window.Title) + "\\z"));
                if (Uri.TryCreate(window.Url, UriKind.Absolute, out var url) && url.Scheme is "https" or "http")
                {
                    choices.Add(new(window.Title, url.AbsoluteUri, RuleMatchType.Url, url.AbsoluteUri));
                    choices.Add(new(url.IdnHost, window.Title, RuleMatchType.Domain, url.IdnHost));
                }
            }
        }
        return choices.DistinctBy(c => (c.MatchType, c.MatchType is RuleMatchType.Process or RuleMatchType.Domain ? c.Pattern.ToUpperInvariant() : c.Pattern))
            .OrderBy(c => c.Label, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static string Normalize(string value) => value.Trim().EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? value.Trim()[..^4] : value.Trim();
}
