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
        (settings, key) => settings.Provider == LlmProvider.Claude
            ? ClaudeClient.Create(key ?? "", settings.ClaudeModel)
            : new OpenAiCompatibleClient(Http, settings.BaseUrl, settings.Model, key);

    // Null when machine translation is not set up. Claude always needs a key.
    public ILlmClient? LlmClient()
    {
        var llm = Settings.Llm;
        if (!llm.IsConfigured)
            return null;
        var key = Credentials.Read(llm.KeyName);
        if (llm.Provider == LlmProvider.Claude && string.IsNullOrEmpty(key))
            return null;
        return CreateLlmClient(llm, key);
    }
}
