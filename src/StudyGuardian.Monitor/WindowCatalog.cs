using StudyGuardian.Core;

namespace StudyGuardian.Monitor;

/// <summary>One-shot enumeration on a user action. Does not start any hooks or read window contents.</summary>
public sealed class WindowCatalog(int maximumTitleCharacters)
{
    public IReadOnlyList<WindowSnapshot> ReadVisibleWindows()
    {
        using var reader = new ForegroundWindowMonitor(maximumTitleCharacters);
        var windows = new List<WindowSnapshot>();
        NativeMethods.EnumWindows((handle, _) =>
        {
            if (NativeMethods.IsWindowVisible(handle))
            {
                var window = reader.ReadWindow(handle);
                if (window.IsAccessible && !string.IsNullOrWhiteSpace(window.Title)) windows.Add(window);
            }
            return true;
        }, 0);
        return windows;
    }
}
