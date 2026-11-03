using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.App;
using StudyGuardian.App.Controls;
using StudyGuardian.Core;
using StudyGuardian.Monitor;

namespace StudyGuardian.Windows.Tests;

[TestClass]
public class FloatingPresentationTests
{
    [TestMethod]
    public void ExpandedFloatingBoundsStayInsideBottomEdgeAtEitherDisplayScale()
    {
        foreach(var scale in new[]{1d,1.5d})
        {
            var work=new Rect(-1920,0,1920,1040);
            var bounds=new Rect(-300,work.Bottom-44*scale,260*scale,112*scale);
            var placed=WindowPlacement.KeepInside(work,bounds);
            Assert.IsTrue(work.Contains(placed));Assert.AreEqual(work.Bottom,placed.Bottom);
            Assert.AreEqual(bounds.Size,placed.Size,"All controls must remain at their requested size.");
        }
    }

    [TestMethod]
    public void NoticeUsesGreatestMonitorIntersectionEvenWhenTopLeftIsOffscreen()
    {
        var primary=new DisplayMonitor(new(0,0,1920,1080),new(0,0,1920,1040),true);
        var secondary=new DisplayMonitor(new(-1280,0,1280,1024),new(-1280,0,1280,984),false);
        foreach(var anchor in new[]{new Rect(-1290,500,260,44),new Rect(-300,990,260,44),new Rect(-200,500,260,44)})
        {
            var monitor=WindowPlacement.SelectMonitor(new[]{primary,secondary},anchor);
            Assert.AreEqual(secondary,monitor);
            var work=new Rect(monitor.WorkArea.Left,monitor.WorkArea.Top,monitor.WorkArea.Width,monitor.WorkArea.Height);
            Assert.IsTrue(work.Contains(NoticePlacement.Calculate(work,new Size(340,180),anchor,8)));
        }
    }

    [TestMethod]
    public Task ExplicitFloatingRequestHasAKeyboardFocusTargetWithoutChangingPassiveDefault() => ExitWorkflowTests.RunUi(() =>
    {
        var floating=new FloatingWindow(1);
        try
        {
            Assert.IsFalse(floating.ShowActivated);
            floating.FocusControls();
            var surface=(FrameworkElement)floating.FindName("Surface");
            Assert.IsTrue(surface.Focusable);Assert.AreSame(surface,System.Windows.Input.FocusManager.GetFocusedElement(floating));
            Assert.IsFalse(floating.ShowActivated,"Automatic presentation must stay passive.");
        }
        finally {floating.ClosePermanently();}
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task LockedOverlayKeepsReturnAvailableAndOnlyRevealsToolsAfterUnlock() => ExitWorkflowTests.RunUi(() =>
    {
        var json=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"config","defaults.json")))!;
        var options=json["intervention"]!.Deserialize<InterventionOptions>(new JsonSerializerOptions {PropertyNameCaseInsensitive=true})!;
        var overlay=new InterventionWindow(InterventionSurface.Block,options,false);
        var state=new EscalationSnapshot(InterventionLevel.Block,true,false,null,TimeSpan.FromSeconds(90),TimeSpan.FromSeconds(90),TimeSpan.FromSeconds(90),TimeSpan.Zero,TimeSpan.FromSeconds(60),1,1);
        var tools=new SessionToolsSnapshot(2,TimeSpan.Zero,TimeSpan.Zero);
        try
        {
            overlay.Update("增值税",state,"Notion.exe",tools,true);
            var buttons=Descendants(overlay.Content as DependencyObject).OfType<Button>().ToArray();
            var back=buttons.Single(b=>Equals(b.Content,"回到 Notion"));
            var appeal=buttons.Single(b=>Equals(b.Content,"申诉"));
            var emergency=buttons.Single(b=>b.Content?.ToString()?.StartsWith("应急通行")==true);
            Assert.IsTrue(back.IsEnabled);Assert.AreEqual(Visibility.Visible,back.Visibility);
            Assert.AreEqual(Visibility.Collapsed,appeal.Visibility);Assert.AreEqual(Visibility.Collapsed,emergency.Visibility);
            overlay.Update("增值税",state with {LockRemaining=TimeSpan.Zero},"Notion.exe",tools,true);
            Assert.AreEqual(Visibility.Visible,appeal.Visibility);Assert.AreEqual(Visibility.Visible,emergency.Visibility);
            Assert.IsTrue(back.IsEnabled);
        }
        finally {overlay.ClosePermanently();}
        return Task.CompletedTask;
    });
    private static IEnumerable<DependencyObject> Descendants(DependencyObject? root)
    {
        if(root is null)yield break;yield return root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Descendants(child))yield return item;
    }
    [TestMethod]
    public void NoticeStaysOnNegativeCoordinateMonitorAndFlipsAboveLowAnchor()
    {
        var work=new Rect(-1920,0,1920,1040);
        var anchor=new Rect(-300,990,260,44);
        var placed=NoticePlacement.Calculate(work,new Size(340,180),anchor,8);
        Assert.IsTrue(work.Contains(placed));Assert.IsTrue(placed.Bottom<=anchor.Top-8);
        var large=NoticePlacement.Calculate(new Rect(0,0,300,180),new Size(340,220),null,8);
        Assert.IsTrue(new Rect(0,0,300,180).Contains(large));
    }

    [TestMethod]
    public Task WarningCountdownAndPausedStateRemainDistinct() => ExitWorkflowTests.RunUi(() =>
    {
        var current=new WindowSnapshot(42,4,"example.exe","synthetic video");
        var snapshot=new StudySessionSnapshot(true,false,false,current,PauseReason.None,new(StudyVerdict.Distracted,DecisionSource.Rule),TimeSpan.FromMinutes(10),.92);
        var warning=new EscalationSnapshot(InterventionLevel.Warning,true,false,current,TimeSpan.FromSeconds(40),TimeSpan.FromSeconds(40),TimeSpan.FromSeconds(40),TimeSpan.FromSeconds(50),TimeSpan.Zero,1,0);
        var floating=new FloatingWindow(1);
        try
        {
            floating.Update(snapshot,"增值税",warning,aiStatus:"AI 已启用");
            Assert.AreEqual(AmbientState.Warn,UiVisuals.StateFor(snapshot,warning));
            Assert.AreEqual("50 秒后拦截",((TextBlock)floating.FindName("TimeText")).Text);
            Assert.AreEqual("AI 已启用",((TextBlock)floating.FindName("AiText")).Text);
            var paused=snapshot with {IsManuallyPaused=true,PauseReason=PauseReason.Manual};
            floating.Update(paused,"增值税",warning);
            Assert.AreEqual("10:00",((TextBlock)floating.FindName("TimeText")).Text);
            Assert.AreEqual(AmbientState.Idle,UiVisuals.StateFor(paused,warning));
        }
        finally {floating.ClosePermanently();}
        return Task.CompletedTask;
    });
}
