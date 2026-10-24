using CatDown.Core.Model;
using CatDown.Core.Storage;

namespace CatDown.Core.Tests.Storage;

public class AppSettingsStoreTests
{
    [Fact]
    public void MissingFileGivesDefaults()
    {
        using var temp = new TempFolder();
        var settings = new AppSettingsStore(temp.Path).Load();
        Assert.Null(settings.UiLanguage);
        Assert.Equal(ThemeChoice.System, settings.Theme);
    }

    [Fact]
    public void RoundTrip()
    {
        using var temp = new TempFolder();
        var store = new AppSettingsStore(Path.Combine(temp.Path, "nested"));
        store.Save(new AppSettings { UiLanguage = "en", Theme = ThemeChoice.Dark });

        var settings = store.Load();
        Assert.Equal("en", settings.UiLanguage);
        Assert.Equal(ThemeChoice.Dark, settings.Theme);
    }

    [Fact]
    public void BrokenFileGivesDefaults()
    {
        using var temp = new TempFolder();
        temp.Write("settings.json", "{{{");
        Assert.Equal(ThemeChoice.System, new AppSettingsStore(temp.Path).Load().Theme);
    }
}
