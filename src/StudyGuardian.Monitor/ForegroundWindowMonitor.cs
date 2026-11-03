using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using StudyGuardian.Core;

namespace StudyGuardian.Monitor;

/// <summary>Start, Stop and Dispose must run on the same thread with a live Windows message loop.</summary>
public sealed class ForegroundWindowMonitor(int maximumTitleCharacters) : IForegroundWindowMonitor
{
    // Strong references are required: native hooks do not root managed delegates.
    private NativeMethods.WinEventCallback? _callback;
    private nint _foregroundHook;
    private nint _titleHook;
    private int _ownerThread;
    private bool _disposed;

    public WindowSnapshot Current { get; private set; } = WindowSnapshot.Unavailable;
    public event EventHandler<WindowSnapshot>? Changed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_foregroundHook != 0) return;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTitleCharacters);
        _ownerThread = Environment.CurrentManagedThreadId;
        _callback = OnWinEvent;
        _foregroundHook = NativeMethods.SetWinEventHook(NativeMethods.EventSystemForeground,
            NativeMethods.EventSystemForeground, 0, _callback, 0, 0, NativeMethods.WinEventOutOfContext);
        if (_foregroundHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法启动前台窗口监听。");
        _titleHook = NativeMethods.SetWinEventHook(NativeMethods.EventObjectNameChange,
            NativeMethods.EventObjectNameChange, 0, _callback, 0, 0, NativeMethods.WinEventOutOfContext);
        if (_titleHook == 0)
        {
            var error = Marshal.GetLastWin32Error();
            Stop();
            throw new Win32Exception(error, "无法启动窗口标题监听。");
        }
        Refresh();
    }

    public WindowSnapshot Refresh()
    {
        Current = ReadWindow(NativeMethods.GetForegroundWindow());
        return Current;
    }

    public void Stop()
    {
        if (_foregroundHook == 0 && _titleHook == 0) return;
        if (Environment.CurrentManagedThreadId != _ownerThread)
            throw new InvalidOperationException("窗口监听必须在启动它的线程停止。");
        if (_titleHook != 0) NativeMethods.UnhookWinEvent(_titleHook);
        if (_foregroundHook != 0) NativeMethods.UnhookWinEvent(_foregroundHook);
        _titleHook = _foregroundHook = 0;
        _callback = null;
    }

    private void OnWinEvent(nint hook, uint eventType, nint window, int objectId, int childId, uint eventThread, uint eventTime)
    {
        if (_foregroundHook == 0) return;
        if (eventType == NativeMethods.EventObjectNameChange &&
            (objectId != 0 || childId != 0 || window != NativeMethods.GetForegroundWindow())) return;
        var next = ReadWindow(window);
        if (next == Current) return;
        Current = next;
        Changed?.Invoke(this, next);
    }

    internal WindowSnapshot ReadWindow(nint window)
    {
        if (window == 0) return WindowSnapshot.Unavailable;
        try
        {
            NativeMethods.GetWindowThreadProcessId(window, out var pid);
            if (pid == 0) return WindowSnapshot.Unavailable;
            using var process = Process.GetProcessById(checked((int)pid));
            var name = process.ProcessName + ".exe";
            var length = Math.Min(Math.Max(NativeMethods.GetWindowTextLength(window), 0), maximumTitleCharacters);
            var title = new StringBuilder(length + 1);
            Marshal.SetLastPInvokeError(0);
            var copied = NativeMethods.GetWindowText(window, title, title.Capacity);
            if (copied == 0 && Marshal.GetLastWin32Error() != 0)
                return new(window.ToInt64(), (int)pid, name, "无法读取窗口标题", false);
            return new(window.ToInt64(), (int)pid, name, title.ToString());
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or OverflowException)
        {
            // A process can exit between the event and the query. Unknown is a pause, not a verdict.
            return new(window.ToInt64(), 0, "", "窗口已关闭或无法读取", false);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
    }
}
