using System.Windows;

namespace StudyGuardian.App.Controls;

public static class NoticePlacement
{
    // All arguments use the same physical-pixel coordinate space.
    public static Rect Calculate(Rect work,Size requested,Rect? anchor,double gap)
    {
        var width=Math.Min(requested.Width,Math.Max(1,work.Width-gap*2));
        var height=Math.Min(requested.Height,Math.Max(1,work.Height-gap*2));
        var left=(anchor?.Right ?? work.Right-gap)-width;
        var top=anchor?.Bottom+gap ?? work.Top+gap;
        if(anchor.HasValue && top+height>work.Bottom-gap)top=anchor.Value.Top-gap-height;
        return new Rect(Math.Clamp(left,work.Left+gap,Math.Max(work.Left+gap,work.Right-gap-width)),Math.Clamp(top,work.Top+gap,Math.Max(work.Top+gap,work.Bottom-gap-height)),width,height);
    }
}
