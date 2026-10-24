using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace CatDown.App.Localization;

// Interface strings. Bindings to Loc.Instance["Key"] refresh when the language changes.
public sealed class Loc : INotifyPropertyChanged
{
    static readonly ResourceManager Resources = new("CatDown.App.Localization.Strings", typeof(Loc).Assembly);

    public static Loc Instance { get; } = new();

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en");

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => Resources.GetString(key, Culture) ?? key;

    public static string Get(string key) => Instance[key];

    public static string Format(string key, params object[] args) => string.Format(Instance.Culture, Instance[key], args);

    // "zh", "en", or null for the system language.
    public static string ResolveLanguage(string? language) =>
        language ?? (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh" : "en");

    public void SetLanguage(string? language)
    {
        Culture = CultureInfo.GetCultureInfo(ResolveLanguage(language) == "zh" ? "zh-CN" : "en");
        // Avalonia's indexer bindings listen for "Item" (and WPF-style code for "Item[]").
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
    }
}
