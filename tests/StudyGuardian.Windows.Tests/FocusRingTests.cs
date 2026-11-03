using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.App.Controls;

namespace StudyGuardian.Windows.Tests;

[TestClass]
public class FocusRingTests
{
    [TestMethod]
    public Task EmptyFullAndInvalidRatesRenderWithoutInvalidGeometry() => ExitWorkflowTests.RunUi(() =>
    {
        var ring=new FocusRing {Width=160,Height=160};
        foreach(var value in new[]{0d,1d,.86d,double.NaN,-1d,2d})
        {
            ring.Progress=value;
            ring.Measure(new Size(160,160));ring.Arrange(new Rect(0,0,160,160));ring.UpdateLayout();
            var bitmap=new RenderTargetBitmap(160,160,96,96,PixelFormats.Pbgra32);bitmap.Render(ring);
            Assert.IsTrue(ring.Progress>=0 && ring.Progress<=1);
        }
        return Task.CompletedTask;
    });
}
