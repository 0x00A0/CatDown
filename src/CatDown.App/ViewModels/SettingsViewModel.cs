using CatDown.App.Services;
using CatDown.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CatDown.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    readonly AppSettings _settings;
    readonly Action<AppSettings> _save;

    public SettingsViewModel(AppSettings settings, Action<AppSettings> save)
    {
        _settings = settings;
        _save = save;
        _selectedLanguage = Languages.First(c => c.Value == settings.UiLanguage);
        _selectedTheme = Themes.First(c => c.Value == settings.Theme);
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

    partial void OnSelectedLanguageChanged(Choice<string?> value)
    {
        _settings.UiLanguage = value.Value;
        Changed();
    }

    partial void OnSelectedThemeChanged(Choice<ThemeChoice> value)
    {
        _settings.Theme = value.Value;
        Changed();
    }

    void Changed()
    {
        Appearance.Apply(_settings);
        _save(_settings);
    }
}
