using System.Text;
using System.Text.Json;
using CatDown.Core.Model;

namespace CatDown.Core.Storage;

public sealed class AppSettingsStore(string folder)
{
    public string Folder { get; } = folder;
    string SettingsPath => Path.Combine(Folder, "settings.json");

    // %APPDATA%/CatDown on Windows, ~/Library/Application Support/CatDown on macOS.
    public static string DefaultFolder()
    {
        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "CatDown");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CatDown");
    }

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();
        try
        {
            return CatDownJson.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = CatDownJson.Serialize(settings) + "\n";
        ProjectStore.AtomicWrite(SettingsPath, stream => stream.Write(new UTF8Encoding(false).GetBytes(json)));
    }
}
