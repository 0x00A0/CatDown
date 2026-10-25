using System.Text.Json.Serialization;

namespace CatDown.Core.Model;

public enum ThemeChoice
{
    System,
    Light,
    Dark,
}

public sealed class AppSettings
{
    // "zh", "en", or null to follow the system language.
    public string? UiLanguage { get; set; }
    public ThemeChoice Theme { get; set; } = ThemeChoice.System;
    public LlmSettings Llm { get; set; } = new();
}

public enum LlmProvider
{
    OpenAiCompatible,
    Claude,
}

// The service used for machine translation. API keys live in the credential store, one per provider.
public sealed class LlmSettings
{
    public const string ApiKeyName = "llm-api-key";
    public const string ClaudeApiKeyName = "claude-api-key";
    public const string DefaultClaudeModel = "claude-opus-5-5";

    public LlmProvider Provider { get; set; } = LlmProvider.OpenAiCompatible;

    // OpenAI-compatible services.
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "";

    // Claude through Anthropic's Messages API.
    public string ClaudeModel { get; set; } = DefaultClaudeModel;

    [JsonIgnore]
    public string KeyName => Provider == LlmProvider.Claude ? ClaudeApiKeyName : ApiKeyName;

    [JsonIgnore]
    public bool IsConfigured => Provider == LlmProvider.Claude
        ? ClaudeModel.Length > 0
        : BaseUrl.Length > 0 && Model.Length > 0;
}
