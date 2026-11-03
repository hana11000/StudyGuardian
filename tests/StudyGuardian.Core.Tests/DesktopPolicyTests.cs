using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;
namespace StudyGuardian.Core.Tests;
[TestClass]
public class DesktopPolicyTests
{
    public static DesktopOptions Options => new() {ExitPhrase="我要结束这次专注",ExitDelaySeconds=60,UiTickMilliseconds=250,WatchdogPollMilliseconds=250,RestartDelayMilliseconds=500,RapidFailureSeconds=60,MaximumRapidRestarts=3};
    [TestMethod] public void CorrectPhraseStartsWaitAndEditingCancelsIt()
    {
        var clock=new FakeClock();var gate=new ExitGate(clock,Options);clock.Advance(60);Assert.IsFalse(gate.CanExit);
        gate.SetText(Options.ExitPhrase);Assert.AreEqual(TimeSpan.FromSeconds(60),gate.Remaining);clock.Advance(59.999);Assert.IsFalse(gate.CanExit);
        clock.Advance(.001);Assert.IsTrue(gate.CanExit);gate.SetText("不同文字");Assert.IsFalse(gate.CanExit);
        gate.SetText(Options.ExitPhrase);Assert.AreEqual(TimeSpan.FromSeconds(60),gate.Remaining);
    }
    [TestMethod] public void RepeatedMatchingInputDoesNotRestartTheWait()
    {
        var clock=new FakeClock();var gate=new ExitGate(clock,Options);gate.SetText(Options.ExitPhrase);clock.Advance(40);gate.SetText(Options.ExitPhrase);
        clock.Advance(20);Assert.IsTrue(gate.CanExit);
    }
    [TestMethod] public void RapidCrashLoopStopsAndStableRunResetsItsBudget()
    {
        var clock=new FakeClock();var budget=new RestartBudget(clock,Options);
        for(var i=0;i<3;i++){clock.Advance(1);Assert.IsTrue(budget.TryRestart());budget.Started();}
        Assert.IsFalse(budget.TryRestart());clock.Advance(60);Assert.IsTrue(budget.TryRestart());
    }
}
