using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StudyGuardian.AI;
using StudyGuardian.Core;

namespace StudyGuardian.Core.Tests;

[TestClass]
public sealed class OpenAiStudyClientTests
{
    [TestMethod]
    [DataRow(400,"模型或请求参数不可用")]
    [DataRow(403,"API 权限不足")]
    [DataRow(404,"模型不可用，请检查名称")]
    public async Task ConfigurationErrorsHaveActionableSafeMessages(int status,string expected)
    {
        using var http=new HttpClient(new Handler((_,_)=>Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
            {Content=new StringContent("private-server-details-must-not-be-shown")})));
        var error=await Assert.ThrowsExactlyAsync<AiUnavailableException>(()=>new OpenAiStudyClient(http,()=>"synthetic-key",AiTestOptions.Create(),"test-model").ClassifyAsync(Request,default));
        Assert.AreEqual(expected,error.Message);
    }
    private const string Decision = """{"verdict":"supportive","confidence":0.93,"reason":"税法讲解","cache_scope":"exact_url","cache_ttl_minutes":30}""";
    private static DecisionRequest Request => new("学习税法", [], new(1234, 5678, "chrome.exe", "增值税", true, "https://example.com/tax"), [], 8);
    private static string Envelope(string payload, string status = "completed") => JsonSerializer.Serialize(new
    { status, output = new[] { new { type = "message", role = "assistant", content = new[] { new { type = "output_text", text = payload } } } } });
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };

    [TestMethod]
    [DataRow(true,.95,true)]
    [DataRow(true,.5,false)]
    [DataRow(false,.95,false)]
    public async Task AppealUsesStrictContractAndRequiresConfidentApproval(bool approved,double confidence,bool expected)
    {
        string? sent=null;
        using var http=new HttpClient(new Handler(async(request,ct)=> {sent=await request.Content!.ReadAsStringAsync(ct);return Json(Envelope(JsonSerializer.Serialize(new{approved,confidence,reason="学习用途"})));}));
        var result=await new OpenAiStudyClient(http,()=>"fake",AiTestOptions.Create(),"test-model").ReviewAppealAsync(new(Request,"查看税法讲解"),default);
        Assert.AreEqual(expected,result.Approved);
        using var body=JsonDocument.Parse(sent!);var format=body.RootElement.GetProperty("text").GetProperty("format");
        Assert.AreEqual("study_appeal",format.GetProperty("name").GetString());Assert.IsTrue(format.GetProperty("strict").GetBoolean());
        Assert.IsFalse(format.GetProperty("schema").GetProperty("additionalProperties").GetBoolean());
    }

    [TestMethod]
    public async Task PrivacyOmitsWindowTitleUrlAndRecentTitlesFromClassification()
    {
        string? sent=null;using var http=new HttpClient(new Handler(async(request,ct)=>{sent=await request.Content!.ReadAsStringAsync(ct);return Json(Envelope(Decision));}));
        await new OpenAiStudyClient(http,()=>"fake",AiTestOptions.Create(),"test-model",new(false,false)).ClassifyAsync(Request with {Recent=[new("Notion.exe","私人笔记",StudyVerdict.OnTask,30)]},default);
        using var body=JsonDocument.Parse(sent!);using var data=JsonDocument.Parse(body.RootElement.GetProperty("input")[1].GetProperty("content").GetString()!);
        Assert.AreEqual("",data.RootElement.GetProperty("current").GetProperty("title").GetString());
        Assert.AreEqual(JsonValueKind.Null,data.RootElement.GetProperty("current").GetProperty("url").ValueKind);
        Assert.AreEqual("",data.RootElement.GetProperty("recent")[0].GetProperty("title").GetString());
    }

    [TestMethod]
    public async Task ResponsesContractUsesStrictSchemaAndOnlyProjectsWindowMetadata()
    {
        string? sent = null;
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            Assert.AreEqual("https://api.openai.com/v1/responses", request.RequestUri!.AbsoluteUri);
            Assert.AreEqual("Bearer", request.Headers.Authorization!.Scheme);
            sent = await request.Content!.ReadAsStringAsync(ct);
            return Json(Envelope(Decision));
        }));
        var client = new OpenAiStudyClient(http, () => "synthetic-test-key", AiTestOptions.Create(), "test-model");
        var result = await client.ClassifyAsync(Request, default);
        Assert.AreEqual(StudyVerdict.Supportive, result.Verdict); Assert.AreEqual(CacheScope.ExactUrl, result.CacheScope);
        using var body = JsonDocument.Parse(sent!);
        Assert.IsFalse(body.RootElement.GetProperty("store").GetBoolean());
        var format = body.RootElement.GetProperty("text").GetProperty("format");
        Assert.AreEqual("json_schema", format.GetProperty("type").GetString()); Assert.IsTrue(format.GetProperty("strict").GetBoolean());
        Assert.IsFalse(format.GetProperty("schema").GetProperty("additionalProperties").GetBoolean());
        using var input = JsonDocument.Parse(body.RootElement.GetProperty("input")[1].GetProperty("content").GetString()!);
        CollectionAssert.AreEquivalent(new[] { "process", "title", "url" }, input.RootElement.GetProperty("current").EnumerateObject().Select(p => p.Name).ToArray());
        Assert.IsFalse(sent!.Contains("synthetic-test-key", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task PolicyProducesEditableValidatedRules()
    {
        const string policy = """{"goal_summary":"学习增值税","rules":[{"kind":"allow","match_type":"process","pattern":"Notion.exe"},{"kind":"supportive","match_type":"title_regex","pattern":"增值税.*讲解"}],"supportive_time_limit_minutes":5}""";
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(Envelope(policy)))));
        var result = await new OpenAiStudyClient(http, () => "fake", AiTestOptions.Create(), "test-model").GeneratePolicyAsync("学习税法", default);
        Assert.HasCount(2, result.Rules); Assert.AreEqual(RuleKind.Supportive, result.Rules[1].Kind);
        Assert.AreNotEqual(result.Rules[0].Id, result.Rules[1].Id); Assert.AreEqual(5, result.SupportiveTimeLimitMinutes);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("not json")]
    [DataRow("{\"verdict\":\"distracted\",\"confidence\":2,\"reason\":\"游戏\",\"cache_scope\":\"domain\",\"cache_ttl_minutes\":30}")]
    [DataRow("{\"verdict\":\"made_up\",\"confidence\":1,\"reason\":\"游戏\",\"cache_scope\":\"domain\",\"cache_ttl_minutes\":30}")]
    public async Task InvalidPayloadFailsClosedToCaller(string payload)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(Envelope(payload)))));
        var client = new OpenAiStudyClient(http, () => "fake", AiTestOptions.Create(), "test-model");
        await Assert.ThrowsExactlyAsync<AiUnavailableException>(() => client.ClassifyAsync(Request, default));
    }

    [TestMethod]
    [DataRow(" chrome.exe ")]
    [DataRow(" CHROME ")]
    [DataRow("\tmsedge.EXE\t")]
    public async Task GeneratedBrowserProcessRuleCannotBypassValidationWithWhitespace(string pattern)
    {
        var policy = JsonSerializer.Serialize(new
        {
            goal_summary = "学习", rules = new[] { new { kind = "allow", match_type = "process", pattern } }, supportive_time_limit_minutes = 5
        });
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(Envelope(policy)))));
        await Assert.ThrowsExactlyAsync<AiUnavailableException>(() => new OpenAiStudyClient(http, () => "fake", AiTestOptions.Create(), "test-model").GeneratePolicyAsync("学习", default));
    }

    [TestMethod]
    [DataRow("{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"refusal\",\"refusal\":\"no\"}]}]}")]
    [DataRow("{\"status\":\"incomplete\",\"output\":[]}")]
    public async Task RefusalAndIncompleteAreUnavailable(string response)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(Json(response))));
        await Assert.ThrowsExactlyAsync<AiUnavailableException>(() => new OpenAiStudyClient(http, () => "fake", AiTestOptions.Create(), "test-model").ClassifyAsync(Request, default));
    }

    [TestMethod]
    public async Task HttpErrorDoesNotExposeResponseOrCredential()
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("private-error-detail") })));
        var error = await Assert.ThrowsExactlyAsync<AiUnavailableException>(() => new OpenAiStudyClient(http, () => "fake", AiTestOptions.Create(), "test-model").ClassifyAsync(Request, default));
        Assert.IsFalse(error.ToString().Contains("private-error-detail", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task TimeoutCoversNetworkAndCallerCancellationRemainsDistinct()
    {
        using var http = new HttpClient(new Handler(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return Json(Envelope(Decision)); }));
        var client = new OpenAiStudyClient(http, () => "fake", AiTestOptions.Create(.05), "test-model");
        await Assert.ThrowsExactlyAsync<AiUnavailableException>(() => client.ClassifyAsync(Request, default));
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => client.ClassifyAsync(Request, cts.Token));
    }

    [TestMethod]
    public async Task MissingKeyMakesNoHttpRequest()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler((_, _) => { calls++; return Task.FromResult(Json(Envelope(Decision))); }));
        await Assert.ThrowsExactlyAsync<AiUnavailableException>(() => new OpenAiStudyClient(http, () => null, AiTestOptions.Create(), "test-model").ClassifyAsync(Request, default));
        Assert.AreEqual(0, calls);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
}
