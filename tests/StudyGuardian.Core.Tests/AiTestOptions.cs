using StudyGuardian.Core;

namespace StudyGuardian.Core.Tests;

internal static class AiTestOptions
{
    public static AiOptions Create(double timeout = 3) => new()
    {
        Model = "test-model", RequestTimeoutSeconds = timeout, CacheMaxTtlMinutes = 30,
        FailureBackoffSeconds = 30, MinimumConfidence = .75, MaximumResponseBytes = 131072,
        MaximumOutputTokens = 1800, MaximumRules = 40, MaximumGoalCharacters = 2000,
        MaximumPatternCharacters = 256, MaximumReasonCharacters = 20, MaximumSupportiveMinutes = 60, DefaultSupportiveMinutes = 5,
        RecentActivityLimit = 5, BrowserProcesses = ["chrome.exe", "msedge.exe"],
        MixedContentDomains = ["bilibili.com", "zhihu.com"]
    };
}
