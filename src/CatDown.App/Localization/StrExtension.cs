using Avalonia.Data;

namespace CatDown.App.Localization;

// {loc:Str Key} in XAML: a localized string that follows language switches.
public sealed class StrExtension(string key)
{
    public string Key { get; set; } = key;

    public object ProvideValue(IServiceProvider serviceProvider) =>
        new ReflectionBinding($"[{Key}]") { Source = Loc.Instance, Mode = BindingMode.OneWay };
}
