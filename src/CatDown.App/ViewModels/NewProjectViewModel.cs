using CatDown.App.Localization;
using CatDown.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CatDown.App.ViewModels;

public partial class NewProjectViewModel : ObservableObject
{
    public NewProjectViewModel()
    {
        Languages = Core.Model.Languages.Supported.Select(code => new Choice<string>(code, $"Language_{code}")).ToList();
        _source = Languages.First(c => c.Value == "en");
        _target = Languages.First(c => c.Value == "zh");
    }

    public IReadOnlyList<Choice<string>> Languages { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(IsValid))]
    private Choice<string> _source;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(IsValid))]
    private Choice<string> _target;

    public bool IsValid => Source.Value != Target.Value;

    public string? Error => IsValid ? null : Loc.Get("SameLanguage");
}
