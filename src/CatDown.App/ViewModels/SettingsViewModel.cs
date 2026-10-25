using CatDown.App.Localization;
using CatDown.App.Services;
using CatDown.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CatDown.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    readonly AppServices _services;
    AppSettings Settings => _services.Settings;

    public SettingsViewModel(AppServices services)
    {
        _services = services;
        _selectedLanguage = Languages.First(c => c.Value == Settings.UiLanguage);
        _selectedTheme = Themes.First(c => c.Value == Settings.Theme);
        _baseUrl = Settings.Llm.BaseUrl;
        _model = Settings.Llm.Model;
        UpdateKeyStatus();
    }

    public IReadOnlyList<Choice<string?>> Languages { get; } =
    [
        new(null, "FollowSystem"),
        new("zh", "Language_zh"),
        new("en", "Language_en"),
    ];

    public IReadOnlyList<Choice<ThemeChoice>> Themes { get; } =
    [
        new(ThemeChoice.System, "FollowSystem"),
        new(ThemeChoice.Light, "ThemeLight"),
        new(ThemeChoice.Dark, "ThemeDark"),
    ];

    [ObservableProperty]
    private Choice<string?> _selectedLanguage;

    [ObservableProperty]
    private Choice<ThemeChoice> _selectedTheme;

    [ObservableProperty]
    private string _baseUrl;

    [ObservableProperty]
    private string _model;

    // Typed in by the user; never filled from the store.
    [ObservableProperty]
    private string _apiKeyInput = "";

    [ObservableProperty]
    private string _keyStatus = "";

    public bool CanStoreKeys => _services.Credentials.IsAvailable;

    partial void OnSelectedLanguageChanged(Choice<string?> value)
    {
        Settings.UiLanguage = value.Value;
        Appearance.Apply(Settings);
        UpdateKeyStatus();
        Save();
    }

    partial void OnSelectedThemeChanged(Choice<ThemeChoice> value)
    {
        Settings.Theme = value.Value;
        Appearance.Apply(Settings);
        Save();
    }

    partial void OnBaseUrlChanged(string value)
    {
        Settings.Llm.BaseUrl = value.Trim();
        Save();
    }

    partial void OnModelChanged(string value)
    {
        Settings.Llm.Model = value.Trim();
        Save();
    }

    [RelayCommand]
    void SaveKey()
    {
        if (!CanStoreKeys || ApiKeyInput.Trim().Length == 0)
            return;
        _services.Credentials.Write(LlmSettings.ApiKeyName, ApiKeyInput.Trim());
        ApiKeyInput = "";
        UpdateKeyStatus();
    }

    [RelayCommand]
    void ClearKey()
    {
        _services.Credentials.Delete(LlmSettings.ApiKeyName);
        UpdateKeyStatus();
    }

    void UpdateKeyStatus() =>
        KeyStatus = !CanStoreKeys ? Loc.Get("KeyUnavailable")
            : _services.Credentials.Read(LlmSettings.ApiKeyName) is { Length: > 0 } ? Loc.Get("KeySaved")
            : Loc.Get("KeyMissing");

    void Save() => _services.SaveSettings(Settings);
}
