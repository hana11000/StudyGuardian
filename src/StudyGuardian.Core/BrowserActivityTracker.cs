namespace StudyGuardian.Core;

public sealed class BrowserActivityTracker(IClock clock, BrowserOptions options)
{
    private sealed record Binding(BrowserActivity Activity, WindowSnapshot Native, string Url, TimeSpan Received);
    private readonly Dictionary<string, (long Sequence, TimeSpan Seen)> _clients = [];
    private readonly Dictionary<string, (BrowserCommand Command, TimeSpan Expires)> _commands = [];
    private Binding? _binding;
    private (int Episode, string Client, int Tab, string Url)? _lastBlocked;

    public bool Accept(BrowserActivity activity, WindowSnapshot foreground)
    {
        if (activity.ClientId is not { Length: 32 } || !activity.ClientId.All(Uri.IsHexDigit) || activity.Sequence <= 0 ||
            !activity.Focused || activity.TabId < 0 || activity.WindowId < 0 || !foreground.IsAccessible ||
            string.IsNullOrWhiteSpace(activity.Title) || activity.Title.Length > options.MaximumTitleCharacters ||
            string.IsNullOrWhiteSpace(activity.Url) || activity.Url.Length > options.MaximumUrlCharacters ||
            !options.Processes.Contains(activity.Process, StringComparer.OrdinalIgnoreCase) ||
            !string.Equals(activity.Process, foreground.ProcessName, StringComparison.OrdinalIgnoreCase) || !TitleMatches(activity.Title, foreground.Title) ||
            !Uri.TryCreate(activity.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        if (_clients.TryGetValue(activity.ClientId, out var previous) && previous.Sequence >= activity.Sequence) return false;
        if (!_clients.ContainsKey(activity.ClientId) && _clients.Count >= options.MaximumClients)
        { var oldest=_clients.MinBy(c=>c.Value.Seen).Key; _clients.Remove(oldest); _commands.Remove(oldest); }
        _clients[activity.ClientId] = (activity.Sequence, clock.Elapsed);
        _binding = new(activity, foreground with { Url = null }, uri.AbsoluteUri, clock.Elapsed);
        return true;
    }

    private bool TitleMatches(string title, string native)
    {
        title=title.Trim(); native=native.Trim();
        return native == title || options.TitleSuffixes.Any(s => string.Equals(native,title+s,StringComparison.Ordinal));
    }

    public WindowSnapshot Enrich(WindowSnapshot foreground)
    {
        if (_binding is not { } binding) return foreground with { Url=null };
        if (clock.Elapsed - binding.Received > TimeSpan.FromSeconds(options.FreshnessSeconds) || binding.Native != (foreground with {Url=null}))
        { _binding=null; return foreground with {Url=null}; }
        return foreground with { Url=binding.Url };
    }

    public void ObserveEscalation(WindowSnapshot window, EscalationSnapshot state, string goal)
    {
        if(state.Level < InterventionLevel.Block) { _commands.Clear(); _lastBlocked=null; return; }
        if (_binding is not { } binding || Enrich(window) != window || window.Url is null || state.Window != window ||
            !state.IsDistracting || state.Level != InterventionLevel.Block) return;
        var identity=(state.EpisodeId, binding.Activity.ClientId, binding.Activity.TabId, binding.Url);
        if (_lastBlocked == identity && _commands.TryGetValue(binding.Activity.ClientId,out var pending) && clock.Elapsed < pending.Expires) return;
        _lastBlocked=identity;
        _commands[binding.Activity.ClientId]=(new("block",binding.Activity.TabId,binding.Url,goal),clock.Elapsed+TimeSpan.FromSeconds(options.CommandTtlSeconds));
    }

    public BrowserCommand GetCommand(string clientId) => _commands.TryGetValue(clientId, out var entry) && clock.Elapsed < entry.Expires ? entry.Command : new();
    public void Clear() { _binding=null; _commands.Clear(); _clients.Clear(); _lastBlocked=null; }
}
