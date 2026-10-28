using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CatDown.App.ViewModels;
using CatDown.App.Views;
using CatDown.Core.Editing;
using CatDown.Core.Glossary;

namespace CatDown.App.Services;

public sealed class AvaloniaDialogService(Window owner, AppServices services) : IDialogService
{
    public async Task<string?> PickFolderAsync(string title)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickFileAsync(string title, string extension)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = [$"*.{extension}"] }],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSavePathAsync(string title, string suggestedName, string extension)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = [$"*.{extension}"] }],
        });
        return file?.TryGetLocalPath();
    }

    public Task<LanguagePair?> AskLanguagesAsync() =>
        new NewProjectDialog { DataContext = new NewProjectViewModel() }.ShowDialog<LanguagePair?>(owner);

    public Task ShowMessageAsync(string title, string message) =>
        new MessageWindow(title, message).ShowDialog(owner);

    public Task<bool> ConfirmAsync(string title, string message) =>
        new MessageWindow(title, message, askToConfirm: true).ShowDialog<bool>(owner);

    public Task<string?> AskTextAsync(string title, string prompt, string initial) =>
        new TextInputWindow(title, prompt, initial).ShowDialog<string?>(owner);

    public Task<string?> EditTextAsync(string title, string text) =>
        new EditTextWindow(title, text).ShowDialog<string?>(owner);

    // Dialogs opened from inside another dialog belong to that dialog, so they stay on top of it.
    public Task<Glossary?> EditGlossaryAsync(Glossary glossary)
    {
        var window = new GlossaryWindow();
        window.DataContext = new GlossaryEditorViewModel(glossary, new AvaloniaDialogService(window, services));
        return window.ShowDialog<Glossary?>(owner);
    }

    public Task ShowSettingsAsync()
    {
        var window = new SettingsWindow();
        window.DataContext = new SettingsViewModel(services, new AvaloniaDialogService(window, services));
        return window.ShowDialog(owner);
    }

    public async Task<ResegmentChoice> AskTranslatedBlockAsync(string paragraph, bool canRealign) =>
        await new ResegmentDialog(paragraph, canRealign).ShowDialog<ResegmentChoice?>(owner)
        ?? new ResegmentChoice(TranslatedBlockAction.Skip, false);
}
