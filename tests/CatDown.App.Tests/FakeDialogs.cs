using CatDown.App.Services;

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
}
