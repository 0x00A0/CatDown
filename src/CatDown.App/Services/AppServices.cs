using CatDown.Core.Llm;
using CatDown.Core.Memory;
using CatDown.Core.Model;
using CatDown.Core.Secrets;

namespace CatDown.App.Services;

// Things shared by every project: app settings, translation memory, secrets, machine translation.
public sealed class AppServices
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };

    public required AppSettings Settings { get; init; }
    public required Action<AppSettings> SaveSettings { get; init; }
    public required TranslationMemory Memory { get; init; }
    public required ICredentialStore Credentials { get; init; }

    public Func<LlmSettings, string?, ILlmClient> CreateLlmClient { get; init; } =
        (settings, key) => new OpenAiCompatibleClient(Http, settings.BaseUrl, settings.Model, key);

    // Null when machine translation is not set up.
    public ILlmClient? LlmClient() =>
        Settings.Llm.IsConfigured ? CreateLlmClient(Settings.Llm, Credentials.Read(LlmSettings.ApiKeyName)) : null;
}
