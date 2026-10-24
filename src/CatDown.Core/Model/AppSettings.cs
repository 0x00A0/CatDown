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

// The OpenAI-compatible service used for machine translation. The API key lives in the credential store.
public sealed class LlmSettings
{
    public const string ApiKeyName = "llm-api-key";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "";

    [JsonIgnore]
    public bool IsConfigured => BaseUrl.Length > 0 && Model.Length > 0;
}
