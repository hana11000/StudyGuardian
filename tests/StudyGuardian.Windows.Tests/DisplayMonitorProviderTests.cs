using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Monitor;

namespace StudyGuardian.Windows.Tests;

[TestClass]
public sealed class DisplayMonitorProviderTests
{
    [TestMethod]
    public void EnumeratesDesktopWithValidPhysicalBounds()
    {
        var monitors = DisplayMonitorProvider.GetMonitors();
        Assert.IsNotEmpty(monitors);
        Assert.AreEqual(1, monitors.Count(m => m.IsPrimary));
        foreach (var monitor in monitors)
        {
            Assert.IsTrue(monitor.Bounds.Width > 0 && monitor.Bounds.Height > 0);
            Assert.IsTrue(monitor.WorkArea.Width > 0 && monitor.WorkArea.Height > 0);
            Assert.IsTrue(monitor.WorkArea.Left >= monitor.Bounds.Left && monitor.WorkArea.Top >= monitor.Bounds.Top);
            Assert.IsTrue(monitor.WorkArea.Left + monitor.WorkArea.Width <= monitor.Bounds.Left + monitor.Bounds.Width);
            Assert.IsTrue(monitor.WorkArea.Top + monitor.WorkArea.Height <= monitor.Bounds.Top + monitor.Bounds.Height);
        }
    }
}
