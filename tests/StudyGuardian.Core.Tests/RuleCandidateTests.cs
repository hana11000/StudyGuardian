using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;

namespace StudyGuardian.Core.Tests;

[TestClass]
public sealed class RuleCandidateTests
{
    [TestMethod]
    public void ApplicationsAreDeduplicatedAndSystemWindowsExcluded()
    {
        var choices=RuleCandidate.FromWindows([
            new(1,2,"Notion.exe","学习"),new(2,3,"NOTION.EXE","另一窗口"),
            new(3,4,"taskmgr.exe","管理器"),new(4,999,"self.exe","本程序"),WindowSnapshot.Unavailable,
            new(5,5,"secret.exe","",false)], ["taskmgr"],["chrome.exe"],999);
        Assert.HasCount(1,choices);
        var rule=choices[0].ToRule(RuleKind.Block);
        Assert.AreEqual(RuleMatchType.Process,rule.MatchType);
        Assert.AreEqual(StudyVerdict.Distracted,Engine(rule).Evaluate(new(1,2,"notion.exe","任意笔记")).Verdict);
    }

    [TestMethod]
    public void BrowserTitleIsLiteralAndDoesNotAllowAllPages()
    {
        var window=new WindowSnapshot(1,2,"chrome.exe","税法 [3] (增值税).* - Google Chrome");
        var choices=RuleCandidate.FromWindows([window],[],["chrome"],999);
        Assert.HasCount(1,choices);
        var rule=choices[0].ToRule(RuleKind.Allow);
        Assert.AreEqual(RuleMatchType.TitleRegex,rule.MatchType);
        Assert.AreEqual(StudyVerdict.OnTask,Engine(rule).Evaluate(window).Verdict);
        Assert.AreEqual(StudyVerdict.Unknown,Engine(rule).Evaluate(window with {Title="游戏视频 - Google Chrome"}).Verdict);
        Assert.AreEqual(StudyVerdict.Unknown,Engine(rule).Evaluate(window with {Title="前缀"+window.Title}).Verdict);
    }

    [TestMethod]
    public void KnownBrowserUrlOffersPageAndDomainAsExplicitSeparateChoices()
    {
        var choices=RuleCandidate.FromWindows([new(1,2,"chrome.exe","课程",true,"https://study.example/course?q=3")],[],["chrome"],999);
        Assert.IsFalse(choices.Any(c=>c.MatchType==RuleMatchType.Process));
        Assert.IsTrue(choices.Any(c=>c.MatchType==RuleMatchType.Url && c.Pattern=="https://study.example/course?q=3"));
        Assert.IsTrue(choices.Any(c=>c.MatchType==RuleMatchType.Domain && c.Pattern=="study.example"));
        Assert.IsTrue(choices.All(c=>RuleEngine.Validate(c.ToRule(RuleKind.Supportive)) is null));
    }

    private static RuleEngine Engine(StudyRule rule)=>new([rule],[],999,TimeSpan.FromMilliseconds(100));
}
