using System.Windows;
using System.Windows.Media;

namespace StudyGuardian.App.Controls;

public sealed class FocusRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty=DependencyProperty.Register(nameof(Progress),typeof(double),typeof(FocusRing),
        new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender,null,(_,value)=>double.IsFinite((double)value)?Math.Clamp((double)value,0,1):0d));
    public static readonly DependencyProperty TrackBrushProperty=DependencyProperty.Register(nameof(TrackBrush),typeof(Brush),typeof(FocusRing),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ProgressBrushProperty=DependencyProperty.Register(nameof(ProgressBrush),typeof(Brush),typeof(FocusRing),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public double Progress {get=>(double)GetValue(ProgressProperty);set=>SetValue(ProgressProperty,value);}
    public Brush TrackBrush {get=>(Brush)GetValue(TrackBrushProperty);set=>SetValue(TrackBrushProperty,value);}
    public Brush ProgressBrush {get=>(Brush)GetValue(ProgressBrushProperty);set=>SetValue(ProgressBrushProperty,value);}
    public FocusRing(){SetResourceReference(TrackBrushProperty,"M8.Subtle");SetResourceReference(ProgressBrushProperty,"M8.FocusGradient");}
    protected override void OnRender(DrawingContext dc)
    {
        var thickness=UiVisuals.Resource<double>("M8.Ring.Thickness");
        var radius=(Math.Min(ActualWidth,ActualHeight)-thickness)/2;if(radius<=0)return;
        var center=new Point(ActualWidth/2,ActualHeight/2);
        dc.DrawEllipse(null,new Pen((Brush)GetValue(TrackBrushProperty),thickness),center,radius,radius);
        if(Progress<=0)return;
        var pen=new Pen((Brush)GetValue(ProgressBrushProperty),thickness){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
        if(Progress>=1){dc.DrawEllipse(null,pen,center,radius,radius);return;}
        var angle=Progress*Math.Tau;
        var end=new Point(center.X+radius*Math.Sin(angle),center.Y-radius*Math.Cos(angle));
        var figure=new PathFigure {StartPoint=new Point(center.X,center.Y-radius),IsClosed=false};
        figure.Segments.Add(new ArcSegment(end,new Size(radius,radius),0,Progress>.5,SweepDirection.Clockwise,true));
        var geometry=new PathGeometry([figure]);geometry.Freeze();dc.DrawGeometry(null,pen,geometry);
    }
}
