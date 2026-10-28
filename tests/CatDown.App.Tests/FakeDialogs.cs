using CatDown.App.Services;
using CatDown.Core.Editing;
using CatDown.Core.Glossary;

namespace CatDown.App.Tests;

public sealed class FakeDialogs : IDialogService
{
    public string? FolderToPick { get; set; }
    public LanguagePair? Languages { get; set; } = new("en", "zh");
    public List<(string Title, string Message)> Messages { get; } = [];
    public int LanguageQuestions { get; private set; }

    public Task<string?> PickFolderAsync(string title) => Task.FromResult(FolderToPick);

    public Task<LanguagePair?> AskLanguagesAsync()
    {
        LanguageQuestions++;
        return Task.FromResult(Languages);
    }

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add((title, message));
        return Task.CompletedTask;
    }

    public Task ShowSettingsAsync() => Task.CompletedTask;

    public Func<string, string?> EditText { get; set; } = text => null;

    public Func<string, string?> AskText { get; set; } = initial => null;

    public Task<string?> AskTextAsync(string title, string prompt, string initial) => Task.FromResult(AskText(initial));

    public Task<string?> EditTextAsync(string title, string text) => Task.FromResult(EditText(text));

    public bool ConfirmAnswer { get; set; } = true;
    public int Confirmations { get; private set; }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        Confirmations++;
        return Task.FromResult(ConfirmAnswer);
    }

    public Func<Glossary, Glossary?> EditGlossary { get; set; } = _ => null;

    public Task<Glossary?> EditGlossaryAsync(Glossary glossary) => Task.FromResult(EditGlossary(glossary));

    public string? FileToPick { get; set; }
    public string? SavePath { get; set; }

    public Task<string?> PickFileAsync(string title, string extension) => Task.FromResult(FileToPick);

    public Task<string?> PickSavePathAsync(string title, string suggestedName, string extension) => Task.FromResult(SavePath);

    public Queue<ResegmentChoice> ResegmentChoices { get; } = [];
    public List<string> AskedParagraphs { get; } = [];

    public Task<ResegmentChoice> AskTranslatedBlockAsync(string paragraph, bool canRealign)
    {
        AskedParagraphs.Add(paragraph);
        return Task.FromResult(ResegmentChoices.Count > 0 ? ResegmentChoices.Dequeue() : new ResegmentChoice(TranslatedBlockAction.Skip, false));
    }
}
