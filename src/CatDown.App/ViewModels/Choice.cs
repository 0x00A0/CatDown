using CatDown.App.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CatDown.App.ViewModels;

// An option in a list whose label is a localized string.
public sealed class Choice<T> : ObservableObject
{
    readonly string _labelKey;

    public Choice(T value, string labelKey)
    {
        Value = value;
        _labelKey = labelKey;
        Loc.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Label));
    }

    public T Value { get; }
    public string Label => Loc.Get(_labelKey);
}
