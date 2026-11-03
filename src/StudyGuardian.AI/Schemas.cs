using StudyGuardian.Core;

namespace StudyGuardian.AI;

internal static class Schemas
{
    private static object Text(params string[] values) => values.Length == 0 ? new { type = "string" } : new { type = "string", @enum = values };
    private static object Object(Dictionary<string, object> properties) => new { type = "object", properties, required = properties.Keys.ToArray(), additionalProperties = false };

    public static object Decision(AiOptions options) => Object(new()
    {
        ["verdict"] = Text("on_task", "supportive", "distracted", "uncertain"),
        ["confidence"] = new { type = "number", minimum = 0, maximum = 1 },
        ["reason"] = Text(),
        ["cache_scope"] = Text("exact_url", "domain", "process", "title"),
        ["cache_ttl_minutes"] = new { type = "integer", minimum = 0, maximum = options.CacheMaxTtlMinutes }
    });

    public static object Policy(AiOptions options) => Object(new()
    {
        ["goal_summary"] = Text(),
        ["rules"] = new { type = "array", items = Object(new()
        {
            ["kind"] = Text("allow", "supportive", "block"),
            ["match_type"] = Text("process", "domain", "url", "title_regex"),
            ["pattern"] = Text()
        }) },
        ["supportive_time_limit_minutes"] = new { type = "integer", minimum = 1, maximum = options.MaximumSupportiveMinutes }
    });

    public static object Appeal() => Object(new()
    {
        ["approved"]=new {type="boolean"},
        ["confidence"]=new {type="number",minimum=0,maximum=1},
        ["reason"]=Text()
    });
}
