using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CatDown.App.Services;
using CatDown.App.ViewModels;
using CatDown.App.Views;
using CatDown.Core.Memory;
using CatDown.Core.Secrets;
using CatDown.Core.Storage;

namespace CatDown.App;

public class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var folder = AppSettingsStore.DefaultFolder();
            var settingsStore = new AppSettingsStore(folder);
            var services = new AppServices
            {
                Settings = settingsStore.Load(),
                SaveSettings = settingsStore.Save,
                Memory = new TranslationMemory(TranslationMemory.DefaultPath(folder)),
                Credentials = CredentialStores.ForCurrentPlatform(),
            };
            Appearance.Apply(services.Settings);

            var window = new MainWindow();
            window.DataContext = new MainWindowViewModel(new AvaloniaDialogService(window, services), services);
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => services.Memory.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
