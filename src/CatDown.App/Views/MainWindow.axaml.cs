using Avalonia.Controls;
using CatDown.App.ViewModels;

namespace CatDown.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Activated += (_, _) => (DataContext as MainWindowViewModel)?.OnActivated();
    }
}
