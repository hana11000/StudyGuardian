using System.ComponentModel;
using System.Runtime.InteropServices;
using StudyGuardian.Core;

namespace StudyGuardian.Monitor;

public sealed class IdleTimeProvider : IIdleTimeProvider
{
    public TimeSpan GetIdleTime()
    {
        var info = new NativeMethods.LastInputInfo { Size = (uint)Marshal.SizeOf<NativeMethods.LastInputInfo>() };
        if (!NativeMethods.GetLastInputInfo(ref info))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法读取空闲时间，记录已暂停。");
        // DWORD subtraction intentionally wraps at 49.7 days; no key contents are read.
        var milliseconds = unchecked((uint)Environment.TickCount - info.Time);
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
