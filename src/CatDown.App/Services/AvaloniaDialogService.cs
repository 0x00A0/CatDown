using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CatDown.App.ViewModels;
using CatDown.App.Views;
using CatDown.Core.Model;

namespace CatDown.App.Services;

public sealed class AvaloniaDialogService(Window owner, AppSettings appSettings, Action<AppSettings> saveSettings) : IDialogService
{
    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public Task<LanguagePair?> AskLanguagesAsync() =>
        new NewProjectDialog { DataContext = new NewProjectViewModel() }.ShowDialog<LanguagePair?>(owner);

    public Task ShowMessageAsync(string title, string message) =>
        new MessageWindow(title, message).ShowDialog(owner);

    public Task<string?> EditTextAsync(string title, string text) =>
        new EditTextWindow(title, text).ShowDialog<string?>(owner);

    public Task ShowSettingsAsync() =>
        new SettingsWindow { DataContext = new SettingsViewModel(appSettings, saveSettings) }.ShowDialog(owner);
}
