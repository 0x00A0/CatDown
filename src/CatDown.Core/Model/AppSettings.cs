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
}
