namespace StudyGuardian.Core;

public sealed record WindowSnapshot(long Handle, int ProcessId, string ProcessName, string Title, bool IsAccessible = true, string? Url = null)
{
    public static WindowSnapshot Unavailable { get; } = new(0, 0, "", "无法读取当前窗口", false);
}
