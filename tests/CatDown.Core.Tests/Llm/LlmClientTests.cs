using System.Net;
using System.Text.Json.Nodes;
using CatDown.Core.Glossary;
using CatDown.Core.Llm;
using CatDown.Core.Secrets;

namespace CatDown.Core.Tests.Llm;

public class LlmClientTests
{
    sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    static string Reply(string content) =>
        new JsonObject { ["choices"] = new JsonArray(new JsonObject { ["message"] = new JsonObject { ["content"] = content } }) }.ToJsonString();

    [Fact]
    public async Task SendsChatCompletionAndReadsReply()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, Reply("  安装 CLI。\n"));
        var client = new OpenAiCompatibleClient(new HttpClient(handler), "https://api.example.com/v1/", "model-x", "secret");

        var result = await client.CompleteAsync([new ChatMessage("user", "hi")], TestContext.Current.CancellationToken);

        Assert.Equal("安装 CLI。", result);
        Assert.Equal("https://api.example.com/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer secret", handler.Request.Headers.Authorization!.ToString());
        var body = JsonNode.Parse(handler.RequestBody!)!;
        Assert.Equal("model-x", body["model"]!.GetValue<string>());
        Assert.Equal("hi", body["messages"]![0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task NoKeyMeansNoAuthorizationHeader()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, Reply("ok"));
        await new OpenAiCompatibleClient(new HttpClient(handler), "http://localhost:11434/v1", "llama", null).CompleteAsync([new ChatMessage("user", "x")], TestContext.Current.CancellationToken);
        Assert.Null(handler.Request!.Headers.Authorization);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{\"error\":\"bad key\"}", "HTTP 401")]
    [InlineData(HttpStatusCode.OK, "{\"choices\":[]}", "empty")]
    [InlineData(HttpStatusCode.OK, "not json", "Unexpected reply")]
    public async Task ErrorsBecomeLlmException(HttpStatusCode status, string body, string expected)
    {
        var client = new OpenAiCompatibleClient(new HttpClient(new FakeHandler(status, body)), "https://x", "m", "k");
        var error = await Assert.ThrowsAsync<LlmException>(() => client.CompleteAsync([new ChatMessage("user", "x")], TestContext.Current.CancellationToken));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void PromptCarriesContextAndGlossary()
    {
        var messages = TranslationPrompt.Build(new TranslationRequest(
            "en", "zh",
            "Then run it.",
            "Install the **CLI**. Then run it.",
            [("Install the **CLI**.", "安装 **CLI**。")],
            [new GlossaryEntry { Source = "CLI", Target = "CLI", Note = "不翻" }]));

        Assert.Equal(["system", "user"], messages.Select(m => m.Role));
        Assert.Contains("from English to Simplified Chinese", messages[0].Content);
        Assert.Contains("Install the **CLI**. Then run it.", messages[1].Content);
        Assert.Contains("- Install the **CLI**. => 安装 **CLI**。", messages[1].Content);
        Assert.Contains("- CLI => CLI (不翻)", messages[1].Content);
        Assert.EndsWith("Sentence to translate:\nThen run it.", messages[1].Content.Replace("\r\n", "\n"));
    }

    [Fact]
    public void PromptWithoutExtrasSkipsSections()
    {
        var user = TranslationPrompt.Build(new TranslationRequest("sv", "en", "Hej.", "Hej.", [], []))[1].Content;
        Assert.DoesNotContain("Glossary", user);
        Assert.DoesNotContain("already translated", user);
    }

    [Theory]
    [InlineData("```\n你好\n```", "Hello", "你好")]
    [InlineData("\"你好\"", "Hello", "你好")]
    [InlineData("“你好”", "Hello", "你好")]
    [InlineData("\"你好\"", "\"Hello\"", "\"你好\"")]
    [InlineData("  你好 ", "Hello", "你好")]
    public void CleansWrappedReplies(string reply, string sentence, string expected) =>
        Assert.Equal(expected, TranslationPrompt.CleanReply(reply, sentence));

    [Fact]
    public void UnavailableStoreReadsNothing()
    {
        var store = new UnavailableCredentialStore();
        Assert.False(store.IsAvailable);
        Assert.Null(store.Read("x"));
        Assert.Throws<InvalidOperationException>(() => store.Write("x", "y"));
    }

    // Writes to the real credential store, so it only runs when asked for.
    [Fact]
    public void PlatformStoreRoundTrip()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("CATDOWN_CREDENTIAL_TESTS") == "1", "Set CATDOWN_CREDENTIAL_TESTS=1 to run.");
        var store = CredentialStores.ForCurrentPlatform();
        var name = "test-" + Guid.NewGuid().ToString("N");
        try
        {
            Assert.Null(store.Read(name));
            store.Write(name, "sk-测试-123");
            Assert.Equal("sk-测试-123", store.Read(name));
            store.Write(name, "second");
            Assert.Equal("second", store.Read(name));
        }
        finally
        {
            store.Delete(name);
        }
        Assert.Null(store.Read(name));
    }
}
