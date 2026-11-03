using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StudyGuardian.Monitor;

public sealed record PixelBounds(int Left, int Top, int Width, int Height);
public sealed record DisplayMonitor(PixelBounds Bounds, PixelBounds WorkArea, bool IsPrimary);

public static class DisplayMonitorProvider
{
    public static IReadOnlyList<DisplayMonitor> GetMonitors()
    {
        List<DisplayMonitor> monitors = [];
        var error = 0;
        bool Callback(nint monitor, nint dc, ref NativeRect rect, nint data)
        {
            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)) { error = Marshal.GetLastWin32Error(); return false; }
            monitors.Add(new(Convert(info.Monitor), Convert(info.Work), (info.Flags & 1) != 0));
            return true;
        }
        MonitorCallback callback = Callback;
        if (!EnumDisplayMonitors(0, 0, callback, 0)) throw new Win32Exception(error == 0 ? Marshal.GetLastWin32Error() : error);
        return monitors;
    }
    private static PixelBounds Convert(NativeRect value) => new(value.Left, value.Top, value.Right - value.Left, value.Bottom - value.Top);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public NativeRect Monitor, Work; public uint Flags; }
    [return: MarshalAs(UnmanagedType.Bool)] private delegate bool MonitorCallback(nint monitor, nint dc, ref NativeRect rect, nint data);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}
