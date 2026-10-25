using Avalonia.Controls;
using Avalonia.Interactivity;
using CatDown.App.ViewModels;

namespace CatDown.App.Views;

public partial class GlossaryWindow : Window
{
    public GlossaryWindow() => InitializeComponent();

    void OnSave(object? sender, RoutedEventArgs e) => Close((DataContext as GlossaryEditorViewModel)?.ToGlossary());

    void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
