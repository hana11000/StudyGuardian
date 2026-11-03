using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;

namespace StudyGuardian.Core.Tests;

[TestClass]
public sealed class RuleEngineTests
{
    private static StudyRule Rule(RuleKind kind, RuleMatchType type, string pattern) => new(Guid.NewGuid().ToString(), kind, type, pattern);
    private static RuleEngine Engine(params StudyRule[] rules) => new(rules, ["taskmgr.exe", "explorer.exe"], 999, TimeSpan.FromMilliseconds(100));
    private static WindowSnapshot Window(string process = "Notion.exe", string title = "第3章 增值税", string? url = null) => new(1, 10, process, title, Url: url);

    [TestMethod]
    public void AllowedProcessMatchesCaseAndOptionalExe()
    {
        var decision = Engine(Rule(RuleKind.Allow, RuleMatchType.Process, "NOTION")).Evaluate(Window());
        Assert.AreEqual(new RuleDecision(StudyVerdict.OnTask, DecisionSource.Rule), decision);
    }

    [TestMethod]
    public void BlockedProcessIsDistracted() => Assert.AreEqual(StudyVerdict.Distracted,
        Engine(Rule(RuleKind.Block, RuleMatchType.Process, "game.exe")).Evaluate(Window("game")).Verdict);

    [TestMethod]
    public void SupportiveRuleIsSupportive() => Assert.AreEqual(StudyVerdict.Supportive,
        Engine(Rule(RuleKind.Supportive, RuleMatchType.Process, "calc.exe")).Evaluate(Window("calc.exe")).Verdict);

    [TestMethod]
    public void ProtectedProcessWinsEvenOverExplicitBlock() => Assert.AreEqual(RuleDecision.Neutral,
        Engine(Rule(RuleKind.Block, RuleMatchType.TitleRegex, ".*")).Evaluate(Window("TASKMGR.exe")));

    [TestMethod]
    public void OwnProcessAndInaccessibleWindowsAreNeutral()
    {
        var engine = Engine(Rule(RuleKind.Block, RuleMatchType.TitleRegex, ".*"));
        Assert.AreEqual(RuleDecision.Neutral, engine.Evaluate(Window() with { ProcessId = 999 }));
        Assert.AreEqual(RuleDecision.Neutral, engine.Evaluate(WindowSnapshot.Unavailable));
    }

    [TestMethod]
    public void NoMatchIsUnknown() => Assert.AreEqual(RuleDecision.Unknown,
        Engine(Rule(RuleKind.Allow, RuleMatchType.Process, "other.exe")).Evaluate(Window()));

    [TestMethod]
    public void TitleRegexMatchesChineseContent() => Assert.AreEqual(StudyVerdict.OnTask,
        Engine(Rule(RuleKind.Allow, RuleMatchType.TitleRegex, "增值税|进项税")).Evaluate(Window()).Verdict);

    [TestMethod]
    [DataRow("https://notion.so/page", StudyVerdict.OnTask)]
    [DataRow("https://team.notion.so/page", StudyVerdict.OnTask)]
    [DataRow("https://NOTION.SO./page", StudyVerdict.OnTask)]
    [DataRow("https://notion.so.evil/page", StudyVerdict.Unknown)]
    [DataRow("https://notnotion.so/page", StudyVerdict.Unknown)]
    [DataRow(null, StudyVerdict.Unknown)]
    public void DomainRequiresRealUrlAndDomainBoundary(string? url, StudyVerdict expected) => Assert.AreEqual(expected,
        Engine(Rule(RuleKind.Allow, RuleMatchType.Domain, "notion.so")).Evaluate(Window(title: "https://notion.so", url: url)).Verdict);

    [TestMethod]
    [DataRow("https://EXAMPLE.com:443/Learn?q=Tax#section", StudyVerdict.OnTask)]
    [DataRow("https://example.com/learn?q=Tax", StudyVerdict.Unknown)]
    [DataRow("https://example.com/Learn?q=Games", StudyVerdict.Unknown)]
    public void ExactUrlRetainsCaseSensitivePathAndQuery(string url, StudyVerdict expected) => Assert.AreEqual(expected,
        Engine(Rule(RuleKind.Allow, RuleMatchType.Url, "https://example.com/Learn?q=Tax")).Evaluate(Window(url: url)).Verdict);

    [TestMethod]
    public void SpecificPageRuleOverridesBroadProcessBlock()
    {
        var engine = Engine(Rule(RuleKind.Block, RuleMatchType.Process, "chrome"),
            Rule(RuleKind.Allow, RuleMatchType.Domain, "bilibili.com"),
            Rule(RuleKind.Block, RuleMatchType.TitleRegex, "游戏"),
            Rule(RuleKind.Supportive, RuleMatchType.Url, "https://bilibili.com/video/tax"));
        Assert.AreEqual(StudyVerdict.Supportive, engine.Evaluate(Window("chrome", "游戏税收", "https://bilibili.com/video/tax")).Verdict);
        Assert.AreEqual(StudyVerdict.Distracted, engine.Evaluate(Window("chrome", "游戏直播", "https://bilibili.com/video/game")).Verdict);
    }

    [TestMethod]
    public void SameSpecificityBlockWinsRegardlessOfOrder()
    {
        var allow = Rule(RuleKind.Allow, RuleMatchType.Process, "notion");
        var block = Rule(RuleKind.Block, RuleMatchType.Process, "notion");
        Assert.AreEqual(StudyVerdict.Distracted, Engine(allow, block).Evaluate(Window()).Verdict);
        Assert.AreEqual(StudyVerdict.Distracted, Engine(block, allow).Evaluate(Window()).Verdict);
    }

    [TestMethod]
    [DataRow(RuleMatchType.TitleRegex, "[")]
    [DataRow(RuleMatchType.Process, " ")]
    [DataRow(RuleMatchType.Domain, "https://notion.so/path")]
    [DataRow(RuleMatchType.Url, "file:///secret.txt")]
    public void InvalidRuleIsRejected(RuleMatchType type, string pattern)
    {
        var rule = Rule(RuleKind.Allow, type, pattern);
        Assert.IsNotNull(RuleEngine.Validate(rule));
        Assert.ThrowsExactly<ArgumentException>(() => Engine(rule));
    }

    [TestMethod]
    public void RegexTimeoutReturnsUnknownInsteadOfFallingThroughToBlock()
    {
        var engine = new RuleEngine([Rule(RuleKind.Allow, RuleMatchType.TitleRegex, "^(a+)+$"),
            Rule(RuleKind.Block, RuleMatchType.Process, "notion")], [], 999, TimeSpan.FromMilliseconds(1));
        Assert.AreEqual(RuleDecision.Unknown, engine.Evaluate(Window(title: new string('a', 10000) + "!")));
    }
}
