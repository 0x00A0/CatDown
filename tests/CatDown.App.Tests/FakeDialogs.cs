using CatDown.App.Services;
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
}
