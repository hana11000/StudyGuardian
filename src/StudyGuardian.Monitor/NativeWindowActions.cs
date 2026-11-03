using System.ComponentModel;
using System.Runtime.InteropServices;
using StudyGuardian.Core;

namespace StudyGuardian.Monitor;

public sealed class NativeWindowActions(IEnumerable<string> protectedProcesses, int ownProcessId, int maximumTitleCharacters)
{
    private readonly WindowActionPolicy _policy = new(protectedProcesses, ownProcessId);
    private readonly ForegroundWindowMonitor _reader = new(maximumTitleCharacters);
    public bool CanIntervene(WindowSnapshot expected) => NativeMethods.GetForegroundWindow() == (nint)expected.Handle &&
        _policy.CanMinimize(expected, _reader.ReadWindow((nint)expected.Handle));

    public bool TryMinimize(WindowSnapshot expected)
    {
        var handle = (nint)expected.Handle;
        if (!CanIntervene(expected)) return false;
        return ShowWindowAsync(handle, 6); // SW_MINIMIZE; no process termination.
    }
    public bool TryActivate(WindowSnapshot expected, WindowSnapshot? avoid = null)
    {
        var handle = (nint)expected.Handle;
        if (!_policy.CanActivate(expected, _reader.ReadWindow(handle), avoid)) return false;
        if (IsIconic(handle)) ShowWindowAsync(handle, 9); // SW_RESTORE
        return SetForegroundWindow(handle);
    }

    public static void SetPhysicalBounds(nint handle, PixelBounds bounds)
    {
        EnsureOwnWindow(handle);
        if (bounds.Width <= 0 || bounds.Height <= 0) throw new ArgumentOutOfRangeException(nameof(bounds));
        if (!SetWindowPos(handle, -1, bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0010 | 0x0040))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public static void ConfigurePassiveWindow(nint handle, bool clickThrough)
    {
        EnsureOwnWindow(handle);
        const int extendedStyle = -20;
        var flags = GetStyle(handle, extendedStyle).ToInt64() | 0x08000000L | 0x00000080L; // NOACTIVATE, TOOLWINDOW
        if (clickThrough) flags |= 0x00000020L; // TRANSPARENT on the WPF layered window
        SetStyle(handle, extendedStyle, (nint)flags);
    }
    private static void EnsureOwnWindow(nint handle)
    {
        NativeMethods.GetWindowThreadProcessId(handle, out var pid);
        if (handle == 0 || pid != Environment.ProcessId) throw new InvalidOperationException("只能定位本程序窗口");
    }
    private static nint GetStyle(nint h, int i) => IntPtr.Size == 8 ? GetWindowLongPtr(h, i) : GetWindowLong(h, i);
    private static void SetStyle(nint h, int i, nint value)
    { if (IntPtr.Size == 8) _ = SetWindowLongPtr(h, i, value); else _ = SetWindowLong(h, i, value.ToInt32()); }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindowAsync(nint window, int command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(nint window, int index, int value);
}
