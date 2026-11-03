using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StudyGuardian.Core.Tests;

[TestClass]
public sealed class SystemPauseStateTests
{
    [TestMethod]
    public void ReconnectingDoesNotUnlockTheSession()
    {
        var state = new SystemPauseState { IsLocked = true };
        state.IsDisconnected = true;
        state.IsDisconnected = false;
        Assert.AreEqual(PauseReason.Locked, state.Reason);
        state.IsLocked = false;
        Assert.IsNull(state.Reason);
    }

    [TestMethod]
    public void UnlockingDoesNotReconnectADisconnectedSession()
    {
        var state = new SystemPauseState { IsLocked = true, IsDisconnected = true };
        state.IsLocked = false;
        Assert.AreEqual(PauseReason.Locked, state.Reason);
        state.IsDisconnected = false;
        Assert.IsNull(state.Reason);
    }

    [TestMethod]
    public void PowerResumePreservesIndependentLockAndConnectionState()
    {
        var state = new SystemPauseState { IsLocked = true, IsDisconnected = true, IsSuspended = true };
        Assert.AreEqual(PauseReason.Suspended, state.Reason);
        state.IsDisconnected = false;
        Assert.AreEqual(PauseReason.Suspended, state.Reason);
        state.IsSuspended = false;
        Assert.AreEqual(PauseReason.Locked, state.Reason);
        state.IsLocked = false;
        Assert.IsNull(state.Reason);
    }
}
