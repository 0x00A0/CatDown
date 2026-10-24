namespace CatDown.App.Services;

public sealed record LanguagePair(string Source, string Target);

public interface IDialogService
{
    Task<string?> PickFolderAsync(string title);
    Task<LanguagePair?> AskLanguagesAsync();
    Task ShowMessageAsync(string title, string message);
    Task ShowSettingsAsync();

    // Lets the user edit a piece of Markdown; null when cancelled.
    Task<string?> EditTextAsync(string title, string text);
}
