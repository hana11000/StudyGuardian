using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.App;
using StudyGuardian.Core;
using StudyGuardian.Monitor;

namespace StudyGuardian.Windows.Tests;

[TestClass]
public class OverlayGeometryTests
{
    [TestMethod]
    public Task HiddenOverlayWindowsUseFullPhysicalMonitorBounds() => ExitWorkflowTests.RunUi(() =>
    {
        var config=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"config","defaults.json")))!;
        var options=config["intervention"]!.Deserialize<InterventionOptions>(new JsonSerializerOptions {PropertyNameCaseInsensitive=true})!;
        foreach(var kind in new[]{InterventionSurface.Block,InterventionSurface.Frame})
        {
            var surface=new InterventionWindow(kind,options,false);
            try
            {
                var handle=new WindowInteropHelper(surface).EnsureHandle();
                // Production placement includes SWP_SHOWWINDOW. Suppress only that flag
                // in this test so the actual native geometry path can run invisibly.
                HwndSource.FromHwnd(handle).AddHook(KeepHidden);
                foreach(var bounds in new[]{new PixelBounds(0,0,1920,1080),new PixelBounds(-1280,0,1280,1024)})
                {
                    surface.Place(new(bounds,bounds,bounds.Left==0),kind==InterventionSurface.Block?InterventionLevel.Block:InterventionLevel.Warning);
                    Assert.IsTrue(GetWindowRect(handle,out var actual));
                    Assert.AreEqual(bounds.Left,actual.Left);Assert.AreEqual(bounds.Top,actual.Top);
                    Assert.AreEqual(bounds.Width,actual.Right-actual.Left);Assert.AreEqual(bounds.Height,actual.Bottom-actual.Top);
                    Assert.IsFalse(IsWindowVisible(handle),"Geometry tests must never display an interception surface.");
                }
            }
            finally {surface.ClosePermanently();}
        }
        return Task.CompletedTask;
    });
    private static nint KeepHidden(nint window,int message,nint wParam,nint lParam,ref bool handled)
    {
        if(message==0x0046) // WM_WINDOWPOSCHANGING
        {
            var position=Marshal.PtrToStructure<WindowPosition>(lParam);
            position.Flags&=~0x0040u; // SWP_SHOWWINDOW
            Marshal.StructureToPtr(position,lParam,false);
        }
        return 0;
    }
    [StructLayout(LayoutKind.Sequential)]private struct WindowPosition {public nint Window,InsertAfter;public int X,Y,Width,Height;public uint Flags;}
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetWindowRect(nint window,out NativeRect bounds);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool IsWindowVisible(nint window);
}
