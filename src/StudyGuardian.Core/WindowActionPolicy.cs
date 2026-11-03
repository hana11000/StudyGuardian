namespace StudyGuardian.Core;

public sealed class WindowActionPolicy(IEnumerable<string> protectedProcesses, int ownProcessId)
{
    private readonly HashSet<string> _protected = new(protectedProcesses.Select(Normalize), StringComparer.OrdinalIgnoreCase);
    public bool CanMinimize(WindowSnapshot expected, WindowSnapshot actual) => CanActivate(expected, actual) && expected.Title == actual.Title;
    public bool CanActivate(WindowSnapshot expected, WindowSnapshot actual, WindowSnapshot? avoid = null) =>
        !(avoid is not null && expected.Handle == avoid.Handle && expected.ProcessId == avoid.ProcessId) &&
        expected.IsAccessible && actual.IsAccessible && expected.Handle != 0 && expected.Handle == actual.Handle &&
        expected.ProcessId > 0 && expected.ProcessId == actual.ProcessId && actual.ProcessId != ownProcessId &&
        !string.IsNullOrWhiteSpace(actual.ProcessName) && !_protected.Contains(Normalize(actual.ProcessName)) &&
        Normalize(expected.ProcessName) == Normalize(actual.ProcessName);
    private static string Normalize(string value)
    {
        var result = value.Trim().ToLowerInvariant();
        return result.EndsWith(".exe", StringComparison.Ordinal) ? result[..^4] : result;
    }
}
