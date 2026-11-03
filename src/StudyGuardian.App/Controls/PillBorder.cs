using System.Windows;
using System.Windows.Controls;

namespace StudyGuardian.App.Controls;

/// <summary>WPF scales oversized corner radii into ellipses; a true pill uses half its height.</summary>
public sealed class PillBorder : Border
{
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        CornerRadius=new CornerRadius(Math.Min(ActualWidth,ActualHeight)/2);
    }
}
