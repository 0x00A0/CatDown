using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using CatDown.App.Localization;
using CatDown.App.Services;
using CatDown.App.ViewModels;
using CatDown.App.Views;
using CatDown.Core.Model;

namespace CatDown.App.Tests;

public class SettingsTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "CatDown.sln")))
            dir = dir.Parent;
        return dir.FullName;
    }

    static List<string> Keys(string file) =>
        XDocument.Load(Path.Combine(RepoRoot(), "src", "CatDown.App", "Localization", file))
            .Root!.Elements("data").Select(e => (string)e.Attribute("name")!).Order().ToList();

    [Fact]
    public void BothLanguagesHaveTheSameKeys() =>
        Assert.Equal(Keys("Strings.resx"), Keys("Strings.zh-CN.resx"));

    [AvaloniaFact]
    public void SwitchingLanguageUpdatesOpenWindows()
    {
        var services = TestServices.Create();
        var settings = services.Settings;
        settings.UiLanguage = "en";
        Loc.Instance.SetLanguage("en");
        var vm = new SettingsViewModel(services);
        var window = new SettingsWindow { DataContext = vm };
        window.Show();
        var label = window.FindControl<TextBlock>("LanguageLabel")!;
        Assert.Equal("Interface language", label.Text);

        vm.SelectedLanguage = vm.Languages.First(c => c.Value == "zh");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("界面语言", label.Text);
        Assert.Equal("zh", settings.UiLanguage);
        Assert.Equal("设置", window.Title);
        Assert.Equal("中文", vm.Languages[1].Label);

        Loc.Instance.SetLanguage("en");
    }

    [AvaloniaFact]
    public void SwitchingThemeChangesVariantAndSaves()
    {
        AppSettings? saved = null;
        var services = TestServices.Create();
        var vm = new SettingsViewModel(new AppServices
        {
            Settings = services.Settings,
            SaveSettings = s => saved = s,
            Memory = services.Memory,
            Credentials = services.Credentials,
        });

        vm.SelectedTheme = vm.Themes.First(c => c.Value == ThemeChoice.Dark);
        Assert.Equal(ThemeVariant.Dark, Application.Current!.ActualThemeVariant);
        Assert.Equal(ThemeChoice.Dark, saved!.Theme);

        vm.SelectedTheme = vm.Themes.First(c => c.Value == ThemeChoice.Light);
        Assert.Equal(ThemeVariant.Light, Application.Current!.ActualThemeVariant);
    }
}
