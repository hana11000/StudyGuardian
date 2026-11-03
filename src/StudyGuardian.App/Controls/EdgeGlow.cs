using System.Windows;
using System.Windows.Media;

namespace StudyGuardian.App.Controls;

public sealed class EdgeGlow : FrameworkElement
{
    public EdgeGlow()=>IsHitTestVisible=false;
    protected override void OnRender(DrawingContext dc)
    {
        var size=Math.Min(UiVisuals.Resource<double>("M8.Warning.EdgeWidth"),Math.Min(ActualWidth,ActualHeight)/2);
        if(size<=0)return;
        var color=UiVisuals.Resource<Color>("M8.Warn.B");var clear=color;clear.A=0;
        LinearGradientBrush Fade(double x1,double y1,double x2,double y2)
        {var brush=new LinearGradientBrush(color,clear,new Point(x1,y1),new Point(x2,y2)){Opacity=UiVisuals.Resource<double>("M8.Warning.EdgeOpacity")};brush.Freeze();return brush;}
        dc.DrawRectangle(Fade(0,0,1,0),null,new Rect(0,0,size,ActualHeight));
        dc.DrawRectangle(Fade(1,0,0,0),null,new Rect(ActualWidth-size,0,size,ActualHeight));
        dc.DrawRectangle(Fade(0,0,0,1),null,new Rect(0,0,ActualWidth,size));
        dc.DrawRectangle(Fade(0,1,0,0),null,new Rect(0,ActualHeight-size,ActualWidth,size));
    }
}
