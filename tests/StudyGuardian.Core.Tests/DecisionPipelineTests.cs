using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.Core;

namespace StudyGuardian.Core.Tests;

[TestClass]
public sealed class DecisionPipelineTests
{
    private FakeClock _clock = null!;
    private FakeAi _ai = null!;
    private MemoryCache _cache = null!;
    private DecisionPipeline _pipeline = null!;
    private static DecisionRequest Request(string title = "增值税", string? url = "https://www.bilibili.com/video/tax") =>
        new("学习税法", [], new(1, 2, "chrome.exe", title, true, url), [], 5);

    [TestInitialize]
    public void SetUp()
    {
        _clock = new(); _ai = new(); _cache = new();
        _pipeline = new(_clock, _ai, _cache, AiTestOptions.Create(), "test-model");
    }

    [TestMethod]
    public async Task LocalRuleAndNeutralBypassAiAndCache()
    {
        foreach (var decision in new[] { RuleDecision.Neutral, new RuleDecision(StudyVerdict.OnTask, DecisionSource.Rule) })
            Assert.AreEqual(decision, await _pipeline.DecideAsync(Request(), decision));
        Assert.AreEqual(0, _ai.Calls); Assert.AreEqual(0, _cache.Reads);
    }

    [TestMethod]
    public async Task SamePageUsesCacheButOtherMixedContentPageDoesNot()
    {
        _ai.Result = new(StudyVerdict.Supportive, .95, "税法讲解", CacheScope.Domain, 30);
        Assert.AreEqual(DecisionSource.Llm, (await _pipeline.DecideAsync(Request(), RuleDecision.Unknown)).Source);
        Assert.AreEqual(DecisionSource.Cache, (await _pipeline.DecideAsync(Request(), RuleDecision.Unknown)).Source);
        _ai.Result = new(StudyVerdict.Distracted, .95, "游戏视频", CacheScope.Domain, 30);
        Assert.AreEqual(StudyVerdict.Distracted, (await _pipeline.DecideAsync(Request("游戏", "https://www.bilibili.com/video/game"), RuleDecision.Unknown)).Verdict);
        Assert.AreEqual(2, _ai.Calls);
    }

    [TestMethod]
    public async Task BrowserProcessScopeCannotAllowEveryPage()
    {
        _ai.Result = _ai.Result with { CacheScope = CacheScope.Process };
        await _pipeline.DecideAsync(Request(), RuleDecision.Unknown);
        await _pipeline.DecideAsync(Request("购物", "https://shop.example/item"), RuleDecision.Unknown);
        Assert.AreEqual(2, _ai.Calls);
    }

    [TestMethod]
    public async Task ExactUrlCacheWinsOverConflictingTitleCache()
    {
        _ai.Result = new(StudyVerdict.Distracted, .95, "游戏视频", CacheScope.ExactUrl, 30);
        var first = Request("视频", "https://www.bilibili.com/video/game");
        await _pipeline.DecideAsync(first, RuleDecision.Unknown);
        _ai.Result = new(StudyVerdict.Supportive, .95, "税法视频", CacheScope.Title, 30);
        await _pipeline.DecideAsync(Request("视频", "https://www.bilibili.com/video/tax"), RuleDecision.Unknown);
        var revisit = await _pipeline.DecideAsync(first, RuleDecision.Unknown);
        Assert.AreEqual(StudyVerdict.Distracted, revisit.Verdict);
        Assert.AreEqual(DecisionSource.Cache, revisit.Source);
        Assert.AreEqual(2, _ai.Calls);
    }

    [TestMethod]
    public async Task GoalRulesAndModelSeparateCaches()
    {
        await _pipeline.DecideAsync(Request(), RuleDecision.Unknown);
        await _pipeline.DecideAsync(Request() with { Goal = "学习音乐" }, RuleDecision.Unknown);
        await _pipeline.DecideAsync(Request() with { Rules = [new("a", RuleKind.Block, RuleMatchType.Process, "game")] }, RuleDecision.Unknown);
        var otherModel = new DecisionPipeline(_clock, _ai, _cache, AiTestOptions.Create(), "other-model");
        await otherModel.DecideAsync(Request(), RuleDecision.Unknown);
        Assert.AreEqual(4, _ai.Calls);
    }

    [TestMethod]
    public async Task ExpiredEntryCallsAiAgain()
    {
        _ai.Result = _ai.Result with { CacheTtlMinutes = 1 };
        await _pipeline.DecideAsync(Request(), RuleDecision.Unknown);
        _clock.Advance(60);
        Assert.AreEqual(DecisionSource.Llm, (await _pipeline.DecideAsync(Request(), RuleDecision.Unknown)).Source);
        Assert.AreEqual(2, _ai.Calls);
    }

    [TestMethod]
    public async Task FailureIsNonEnforcingSupportiveAndBacksOffWithoutCaching()
    {
        _ai.Fail = true;
        var result = await _pipeline.DecideAsync(Request(), RuleDecision.Unknown);
        Assert.AreEqual(StudyVerdict.Supportive, result.Verdict);
        Assert.AreEqual(DecisionSource.Fallback, result.Source);
        Assert.IsFalse(result.CanEnforce);
        await _pipeline.DecideAsync(Request("第二个窗口"), RuleDecision.Unknown);
        Assert.AreEqual(1, _ai.Calls); Assert.HasCount(0, _cache.Entries);
        _clock.Advance(30); _ai.Fail = false;
        Assert.AreEqual(DecisionSource.Llm, (await _pipeline.DecideAsync(Request(), RuleDecision.Unknown)).Source);
    }

    [TestMethod]
    public async Task UncertainAndLowConfidenceNeverEnforce()
    {
        _ai.Result = new(StudyVerdict.Distracted, .2, "不确定", CacheScope.Title, 0);
        var result = await _pipeline.DecideAsync(Request(), RuleDecision.Unknown);
        Assert.AreEqual(StudyVerdict.Uncertain, result.Verdict); Assert.IsFalse(result.CanEnforce);
        _ai.Result = _ai.Result with { Verdict = StudyVerdict.Uncertain, Confidence = 1 };
        Assert.IsFalse((await _pipeline.DecideAsync(Request(), RuleDecision.Unknown)).CanEnforce);
    }

    [TestMethod]
    public async Task CallerCancellationIsNotConvertedToFailure()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => _pipeline.DecideAsync(Request(), RuleDecision.Unknown, cts.Token));
        Assert.AreEqual(0, _ai.Calls);
    }

    [TestMethod]
    public async Task NormalizedTitleStillPreservesMeaningAndUrlPathCase()
    {
        _ai.Result = _ai.Result with { CacheScope = CacheScope.Title };
        await _pipeline.DecideAsync(Request("增值税  第一章", null), RuleDecision.Unknown);
        Assert.AreEqual(DecisionSource.Cache, (await _pipeline.DecideAsync(Request("增值税 第一章", null), RuleDecision.Unknown)).Source);
        await _pipeline.DecideAsync(Request("游戏 第一章", null), RuleDecision.Unknown);
        Assert.AreEqual(2, _ai.Calls);
    }

    private sealed class FakeAi : IStudyAi
    {
        public int Calls; public bool Fail;
        public AiDecision Result = new(StudyVerdict.OnTask, .95, "正在学习", CacheScope.ExactUrl, 30);
        public Task<AiDecision> ClassifyAsync(DecisionRequest request, CancellationToken cancellationToken)
        { Calls++; if (Fail) throw new AiUnavailableException("服务不可用"); return Task.FromResult(Result); }
        public Task<StudyPolicy> GeneratePolicyAsync(string goal, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private sealed class MemoryCache : IDecisionCache
    {
        public int Reads; public Dictionary<string, CachedDecision> Entries = [];
        public Task<CachedDecision?> FindDecisionAsync(string key, DateTimeOffset now, CancellationToken cancellationToken)
        { Reads++; return Task.FromResult(Entries.TryGetValue(key, out var v) && v.ExpiresAt > now ? v : null); }
        public Task SaveDecisionAsync(CachedDecision entry, CancellationToken cancellationToken)
        { Entries[entry.Key] = entry; return Task.CompletedTask; }
    }
}
