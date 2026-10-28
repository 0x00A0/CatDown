using CatDown.Core.Editing;
using CatDown.Core.Glossary;

namespace CatDown.App.Services;

public sealed record LanguagePair(string Source, string Target);

public sealed record ResegmentChoice(TranslatedBlockAction Action, bool ApplyToAll);

public interface IDialogService
{
    Task<string?> PickFolderAsync(string title);

    // An existing file with one of the extensions (without dots); null when cancelled.
    Task<string?> PickFileAsync(string title, string extension);

    // Where to save a new file; null when cancelled.
    Task<string?> PickSavePathAsync(string title, string suggestedName, string extension);

    Task<LanguagePair?> AskLanguagesAsync();
    Task ShowMessageAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message);
    Task ShowSettingsAsync();

    // A one-line answer such as a file name; null when cancelled.
    Task<string?> AskTextAsync(string title, string prompt, string initial);

    // Lets the user edit a piece of Markdown; null when cancelled.
    Task<string?> EditTextAsync(string title, string text);

    // Edits the glossary; returns the new glossary, or null when cancelled.
    Task<Glossary?> EditGlossaryAsync(Glossary glossary);

    // A paragraph with translations is about to be split again; asks what to do with them.
    Task<ResegmentChoice> AskTranslatedBlockAsync(string paragraph, bool canRealign);
}
