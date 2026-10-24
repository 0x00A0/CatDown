using Avalonia;
using Avalonia.Styling;
using CatDown.App.Localization;
using CatDown.Core.Model;
using Semi.Avalonia;

namespace CatDown.App.Services;

// Applies the interface language and theme from the app settings.
public static class Appearance
{
    public static void Apply(AppSettings settings)
    {
        Loc.Instance.SetLanguage(settings.UiLanguage);
        if (Application.Current is not { } app)
            return;

        app.RequestedThemeVariant = settings.Theme switch
        {
            ThemeChoice.Light => ThemeVariant.Light,
            ThemeChoice.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
        foreach (var style in app.Styles)
        {
            if (style is SemiTheme semi)
                semi.Locale = Loc.Instance.Culture;
        }
    }
}
