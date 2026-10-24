using Avalonia.Controls;
using Avalonia.Interactivity;
using CatDown.App.Services;
using CatDown.App.ViewModels;

namespace CatDown.App.Views;

public partial class NewProjectDialog : Window
{
    public NewProjectDialog() => InitializeComponent();

    void OnOk(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NewProjectViewModel { IsValid: true } vm)
            Close(new LanguagePair(vm.Source.Value, vm.Target.Value));
    }

    void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
