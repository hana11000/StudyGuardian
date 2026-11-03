using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;

namespace StudyGuardian.Core.Tests;

[TestClass]
public sealed class WindowActionPolicyTests
{
    private readonly WindowActionPolicy _policy = new(["taskmgr.exe", "explorer", "SystemSettings.exe", "StudyGuardian.App.exe"], 999);
    private static readonly WindowSnapshot Game = new(1, 2, "game.exe", "游戏");
    [TestMethod] public void ExactLiveUnprotectedTargetCanBeMinimized() => Assert.IsTrue(_policy.CanMinimize(Game, Game));
    [TestMethod]
    [DataRow("taskmgr.exe")][DataRow(" explorer.EXE ")][DataRow("SystemSettings")][DataRow("StudyGuardian.App.exe")]
    public void ProtectedProcessesAreNeverMinimized(string process)
    { var window = Game with { ProcessName = process }; Assert.IsFalse(_policy.CanMinimize(window, window)); }
    [TestMethod] public void OwnProcessAndUnreadableTargetAreProtected()
    {
        var own = Game with { ProcessId = 999 }; Assert.IsFalse(_policy.CanMinimize(own, own));
        Assert.IsFalse(_policy.CanMinimize(Game, Game with { IsAccessible = false }));
        Assert.IsFalse(_policy.CanMinimize(WindowSnapshot.Unavailable, WindowSnapshot.Unavailable));
    }
    [TestMethod] public void ReusedHandleOrChangedContentCannotBeMinimized()
    {
        Assert.IsFalse(_policy.CanMinimize(Game, Game with { ProcessId = 3 }));
        Assert.IsFalse(_policy.CanMinimize(Game, Game with { ProcessName = "study.exe" }));
        Assert.IsFalse(_policy.CanMinimize(Game, Game with { Title = "学习页面" }));
    }
    [TestMethod] public void ReturningToExistingStudyWindowAllowsUpdatedTitle()
    { Assert.IsTrue(_policy.CanActivate(Game, Game with { Title = "更新后的标题" })); }
    [TestMethod] public void SameBrowserWindowThatBecameDistractingCannotBeReturnDestination()
    {
        var study = Game with { ProcessName = "chrome.exe", Title = "增值税学习" };
        var blocked = study with { Title = "游戏直播" };
        Assert.IsFalse(_policy.CanActivate(study, blocked, blocked));
    }
    [TestMethod] public void SeparateStudyWindowRemainsAValidReturnDestination()
    {
        var study = Game with { Handle = 2, Title = "学习" };
        Assert.IsTrue(_policy.CanActivate(study, study, Game));
    }
}
