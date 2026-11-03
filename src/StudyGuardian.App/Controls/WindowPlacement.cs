using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using StudyGuardian.Monitor;

namespace StudyGuardian.App.Controls;

public static class WindowPlacement
{
    // Physical pixels throughout, including mixed-DPI and negative-coordinate displays.
    public static Rect KeepInside(Rect work,Rect bounds) => new(
        Math.Clamp(bounds.Left,work.Left,Math.Max(work.Left,work.Right-bounds.Width)),
        Math.Clamp(bounds.Top,work.Top,Math.Max(work.Top,work.Bottom-bounds.Height)),
        bounds.Width,bounds.Height);

    public static DisplayMonitor SelectMonitor(IReadOnlyList<DisplayMonitor> monitors,Rect? anchor)
    {
        if(monitors.Count==0)throw new ArgumentException("没有可用显示器",nameof(monitors));
        if(anchor is not { } target)return monitors.FirstOrDefault(m=>m.IsPrimary)??monitors[0];
        return monitors.OrderByDescending(m=>Intersection(m.Bounds,target))
            .ThenBy(m=>Distance(m.Bounds,target)).ThenByDescending(m=>m.IsPrimary).First();
    }
    private static double Intersection(PixelBounds bounds,Rect target)
    {
        var overlap=Rect.Intersect(new(bounds.Left,bounds.Top,bounds.Width,bounds.Height),target);
        return overlap.IsEmpty?0:overlap.Width*overlap.Height;
    }
    private static double Distance(PixelBounds bounds,Rect target)
    {
        var dx=Math.Max(0,Math.Max(bounds.Left-target.Right,target.Left-(bounds.Left+bounds.Width)));
        var dy=Math.Max(0,Math.Max(bounds.Top-target.Bottom,target.Top-(bounds.Top+bounds.Height)));
        return dx*dx+dy*dy;
    }
    public static void Constrain(Window window)
    {
        var handle=new WindowInteropHelper(window).Handle;
        if(handle==0 || !GetWindowRect(handle,out var native) || native.Right<=native.Left || native.Bottom<=native.Top)return;
        try
        {
            var bounds=new Rect(native.Left,native.Top,native.Right-native.Left,native.Bottom-native.Top);
            var monitors=DisplayMonitorProvider.GetMonitors();if(monitors.Count==0)return;
            var work=SelectMonitor(monitors,bounds).WorkArea;
            var placed=KeepInside(new(work.Left,work.Top,work.Width,work.Height),bounds);
            if(placed.Location!=bounds.Location)
                // Move only: preserve the WPF height animation, activation and visibility.
                _=SetWindowPos(handle,0,(int)Math.Round(placed.Left),(int)Math.Round(placed.Top),0,0,0x0015);
        }
        catch(Win32Exception) { /* Keep the current position if the display topology is temporarily unavailable. */ }
    }
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetWindowRect(nint window,out NativeRect bounds);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
}
