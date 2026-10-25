using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CatDown.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow() => InitializeComponent();

    void OnClose(object? sender, RoutedEventArgs e) => Close();
}
