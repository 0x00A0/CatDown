using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CatDown.App.Services;
using CatDown.App.ViewModels;
using CatDown.App.Views;
using CatDown.Core.Storage;

namespace CatDown.App;

public class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsStore = new AppSettingsStore(AppSettingsStore.DefaultFolder());
            var settings = settingsStore.Load();
            Appearance.Apply(settings);

            var window = new MainWindow();
            window.DataContext = new MainWindowViewModel(new AvaloniaDialogService(window, settings, settingsStore.Save));
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
