using CommunityToolkit.Mvvm.ComponentModel;

namespace CatDown.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "CatDown";
}
