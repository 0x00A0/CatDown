using System.Net;
using System.Text.Json.Nodes;
using CatDown.Core.Llm;
using CatDown.Core.Model;
using CatDown.Core.Storage;

namespace CatDown.Core.Tests.Llm;

public class ClaudeClientTests
{
    sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public JsonNode? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
        }
    }

    static string Message(string stopReason, params JsonObject[] content) => new JsonObject
    {
        ["id"] = "msg_test",
        ["type"] = "message",
        ["role"] = "assistant",
        ["model"] = "claude-opus-5-5",
        ["content"] = new JsonArray(content.Select(c => (JsonNode)c).ToArray()),
        ["stop_reason"] = stopReason,
        ["stop_sequence"] = null,
        ["usage"] = new JsonObject { ["input_tokens"] = 10, ["output_tokens"] = 5 },
    }.ToJsonString();

    static JsonObject Text(string text) => new() { ["type"] = "text", ["text"] = text };

    static readonly IReadOnlyList<ChatMessage> Prompt = [new("system", "Translate."), new("user", "Hello.")];

    [Fact]
    public async Task SendsMessagesRequestWithFallbacksAndNoTemperature()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, Message("end_turn",
            new JsonObject { ["type"] = "thinking", ["thinking"] = "", ["signature"] = "sig" },
            Text("  你好。 ")));
        var client = ClaudeClient.Create("test-key", "claude-opus-5-5", new HttpClient(handler));

        var reply = await client.CompleteAsync(Prompt, TestContext.Current.CancellationToken);

        Assert.Equal("你好。", reply);
        Assert.Equal("https://api.anthropic.com/v1/messages", handler.Request!.RequestUri!.GetLeftPart(UriPartial.Path));
        Assert.Equal("Bearer test-key", handler.Request.Headers.Authorization!.ToString());
        Assert.Contains(ClaudeClient.FallbackBeta, string.Join(",", handler.Request.Headers.GetValues("anthropic-beta")));
        var body = handler.RequestBody!;
        Assert.Equal("claude-opus-5-5", body["model"]!.GetValue<string>());
        Assert.Equal("Translate.", body["system"]!.GetValue<string>());
        Assert.Equal("user", body["messages"]![0]!["role"]!.GetValue<string>());
        Assert.Equal("Hello.", body["messages"]![0]!["content"]!.GetValue<string>());
        Assert.Single(body["messages"]!.AsArray());
        Assert.Equal("medium", body["output_config"]!["effort"]!.GetValue<string>());
        Assert.Equal("default", body["fallbacks"]!.GetValue<string>());
        Assert.Null(body["temperature"]);
        Assert.Null(body["thinking"]);
    }

    [Fact]
    public async Task HaikuGetsNoFallbacks()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, Message("end_turn", Text("ok")));
        await ClaudeClient.Create("k", "claude-haiku-5-5", new HttpClient(handler)).CompleteAsync(Prompt, TestContext.Current.CancellationToken);

        Assert.Null(handler.RequestBody!["fallbacks"]);
        Assert.False(handler.Request!.Headers.TryGetValues("anthropic-beta", out var betas) && betas.Any(b => b.Contains(ClaudeClient.FallbackBeta)));
    }

    [Fact]
    public async Task RefusalBecomesLlmException()
    {
        var body = JsonNode.Parse(Message("refusal"))!.AsObject();
        body["stop_details"] = new JsonObject { ["type"] = "refusal", ["category"] = "cyber", ["explanation"] = "Policy." };
        var client = ClaudeClient.Create("k", "claude-opus-5-5", new HttpClient(new FakeHandler(HttpStatusCode.OK, body.ToJsonString())));

        var error = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(Prompt, TestContext.Current.CancellationToken));
        Assert.Contains("Policy.", error.Message);
    }

    [Fact]
    public async Task ApiErrorBecomesLlmException()
    {
        const string error = """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""";
        var client = ClaudeClient.Create("bad", "claude-opus-5-5", new HttpClient(new FakeHandler(HttpStatusCode.Unauthorized, error)));

        var exception = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(Prompt, TestContext.Current.CancellationToken));
        Assert.Contains("invalid x-api-key", exception.Message);
    }

    [Fact]
    public async Task EmptyReplyBecomesLlmException()
    {
        var client = ClaudeClient.Create("k", "claude-opus-5-5", new HttpClient(new FakeHandler(HttpStatusCode.OK, Message("end_turn"))));
        await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync(Prompt, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SettingsKeepOldFilesWorking()
    {
        var old = CatDownJson.Deserialize<AppSettings>("""{ "llm": { "baseUrl": "https://x/v1", "model": "m" } }""");
        Assert.Equal(LlmProvider.OpenAiCompatible, old.Llm.Provider);
        Assert.Equal(LlmSettings.DefaultClaudeModel, old.Llm.ClaudeModel);
        Assert.Equal(LlmSettings.ApiKeyName, old.Llm.KeyName);

        old.Llm.Provider = LlmProvider.Claude;
        Assert.Equal(LlmSettings.ClaudeApiKeyName, old.Llm.KeyName);
        Assert.Contains("\"provider\": \"claude\"", CatDownJson.Serialize(old));
    }
}
