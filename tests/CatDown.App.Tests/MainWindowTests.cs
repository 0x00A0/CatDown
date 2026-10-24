using Avalonia.Headless.XUnit;
using CatDown.App.ViewModels;
using CatDown.App.Views;

namespace CatDown.App.Tests;

public class MainWindowTests
{
    [AvaloniaFact]
    public void MainWindowOpens()
    {
        var window = new MainWindow { DataContext = new MainWindowViewModel() };
        window.Show();
        Assert.True(window.IsVisible);
        Assert.Equal("CatDown", window.Title);
    }
}
