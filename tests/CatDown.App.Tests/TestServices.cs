using CatDown.App.Services;
using CatDown.Core.Llm;
using CatDown.Core.Memory;
using CatDown.Core.Model;
using CatDown.Core.Secrets;

namespace CatDown.App.Tests;

public sealed class FakeCredentials : ICredentialStore
{
    public Dictionary<string, string> Values { get; } = [];
    public bool IsAvailable { get; set; } = true;
    public string? Read(string name) => Values.GetValueOrDefault(name);
    public void Write(string name, string secret) => Values[name] = secret;
    public void Delete(string name) => Values.Remove(name);
}

public sealed class FakeLlm : ILlmClient
{
    public string Reply { get; set; } = "机翻结果。";
    public Exception? Error { get; set; }
    public List<IReadOnlyList<ChatMessage>> Calls { get; } = [];

    public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        Calls.Add(messages);
        return Error is null ? Task.FromResult(Reply) : Task.FromException<string>(Error);
    }
}

public static class TestServices
{
    // Services with a fresh translation memory in a temporary file and fake secrets and model.
    public static AppServices Create(FakeLlm? llm = null, string? apiKeyUsed = null)
    {
        var tm = Path.Combine(Path.GetTempPath(), "catdown-app-tests", $"tm-{Guid.NewGuid():N}.sqlite");
        var fake = llm ?? new FakeLlm();
        return new AppServices
        {
            Settings = new AppSettings(),
            SaveSettings = _ => { },
            Memory = new TranslationMemory(tm),
            Credentials = new FakeCredentials(),
            CreateLlmClient = (_, key) =>
            {
                LastKey = key;
                return fake;
            },
        };
    }

    public static string? LastKey { get; private set; }
}
