using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CatDown.App.Localization;
using CatDown.App.Services;
using CatDown.App.ViewModels;
using CatDown.App.Views;
using CatDown.Core.Llm;
using CatDown.Core.Model;

namespace CatDown.App.Tests;

public class BatchOperationsTests
{
    sealed record Harness(MainWindow Window, MainWindowViewModel Vm, FakeDialogs Dialogs, AppServices Services)
    {
        public SegmentGridViewModel Grid => Vm.Editor!;
    }

    static async Task<Harness> Open(TestProject project, string? file = "README.md", AppServices? services = null)
    {
        Loc.Instance.SetLanguage("en");
        services ??= TestServices.Create();
        services.Settings.Llm.BaseUrl = "https://x";
        services.Settings.Llm.Model = "m";
        var dialogs = new FakeDialogs();
        var vm = new MainWindowViewModel(dialogs, services);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        await vm.OpenProject(project.Path);
        if (file is not null)
            vm.OpenFile(file);
        Dispatcher.UIThread.RunJobs();
        return new Harness(window, vm, dialogs, services);
    }

    static void Select(SegmentGridViewModel grid, params int[] indexes)
    {
        grid.SelectedRows.Clear();
        grid.SelectedRow = grid.Rows[indexes[0]];
        foreach (var i in indexes)
            grid.SelectedRows.Add(grid.Rows[i]);
    }

    [AvaloniaFact]
    public async Task NewFileIsCreatedNextToTheOpenFileAndOpened()
    {
        using var project = new TestProject();
        var h = await Open(project, "guide/api.md");
        string? suggested = null;
        h.Dialogs.AskText = initial => { suggested = initial; return "guide/intro"; };
        h.Dialogs.EditText = _ => "# Intro\n\nOne. Two.\n";

        await h.Vm.NewFileCommand.ExecuteAsync(null);

        Assert.Equal("guide/untitled.md", suggested);
        Assert.Equal("# Intro\n\nOne. Two.\n", project.Read("guide/intro.md"));
        Assert.Equal("guide/intro.md", h.Vm.Session!.RelativePath);
        Assert.Equal(["Intro", "One.", "Two."], h.Grid.Rows.Select(r => r.Source));
        Assert.Contains(h.Vm.Files.Single(f => f.Name == "guide").Children, c => c.Name == "intro.md");
    }

    [AvaloniaFact]
    public async Task NewFileRejectsBadNamesAndExistingFiles()
    {
        using var project = new TestProject();
        var h = await Open(project);
        h.Dialogs.EditText = _ => "x";

        h.Dialogs.AskText = _ => "../outside.md";
        await h.Vm.NewFileCommand.ExecuteAsync(null);
        h.Dialogs.AskText = _ => "README.md";
        await h.Vm.NewFileCommand.ExecuteAsync(null);

        Assert.Equal([Loc.Get("NewFileInvalid"), Loc.Format("NewFileExists", "README.md")], h.Dialogs.Messages.Select(m => m.Message));
        Assert.False(File.Exists(Path.Combine(project.Path, "..", "outside.md")));
    }

    [AvaloniaFact]
    public async Task EmptyNewFileGetsTextThroughWholeSourceEditing()
    {
        using var project = new TestProject();
        var h = await Open(project);
        h.Dialogs.AskText = _ => "empty.md";
        h.Dialogs.EditText = _ => "";
        await h.Vm.NewFileCommand.ExecuteAsync(null);
        Assert.Empty(h.Grid.Rows);

        h.Dialogs.EditText = _ => "Now some text. More.";
        await h.Grid.EditWholeSourceCommand.ExecuteAsync(null);

        Assert.Equal("Now some text. More.", project.Read("empty.md"));
        Assert.Equal(["Now some text.", "More."], h.Grid.Rows.Select(r => r.Source));
        Assert.Same(h.Grid.Rows[0], h.Grid.SelectedRow);
    }

    [AvaloniaFact]
    public async Task ListSelectionFeedsSelectedRows()
    {
        using var project = new TestProject();
        var h = await Open(project);
        var list = h.Window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "RowList");

        list.SelectedItems!.Add(h.Grid.Rows[2]);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, h.Grid.SelectedRows.Count);
        Assert.Equal([h.Grid.Rows[0], h.Grid.Rows[2]], h.Grid.TargetRows());
    }

    [AvaloniaFact]
    public async Task ConfirmAndClearSeveralRows()
    {
        using var project = new TestProject();
        var h = await Open(project);
        h.Grid.Rows[1].Target = "第一句。";
        h.Grid.Rows[2].Target = "第二句。";
        Select(h.Grid, 1, 2);

        h.Grid.ConfirmCommand.Execute(null);

        Assert.True(h.Grid.Rows[1].IsConfirmed);
        Assert.True(h.Grid.Rows[2].IsConfirmed);
        Assert.Equal(2, h.Services.Memory.Count());

        Select(h.Grid, 1, 2);
        h.Grid.ClearTargetsCommand.Execute(null);
        Assert.All(h.Grid.Rows.Skip(1), r => Assert.True(r.IsUntranslated));
    }

    [AvaloniaFact]
    public async Task MergeSeveralNeighbours()
    {
        using var project = new TestProject();
        project.Write("README.md", "One. Two. Three. Four.\n\nOther.\n");
        var h = await Open(project);
        Select(h.Grid, 1, 2, 0);

        h.Grid.MergeCommand.Execute(null);

        Assert.Equal(["One. Two. Three.", "Four.", "Other."], h.Grid.Rows.Select(r => r.Source));
        Assert.Equal("One. Two. Three.", h.Grid.SelectedRow!.Source);
    }

    [AvaloniaFact]
    public async Task MergeRefusesGapsAndOtherParagraphs()
    {
        using var project = new TestProject();
        project.Write("README.md", "One. Two. Three.\n\nOther.\n");
        var h = await Open(project);

        Select(h.Grid, 0, 2);
        h.Grid.MergeCommand.Execute(null);
        Assert.Equal(Loc.Get("MergeNotContiguous"), h.Vm.Notice);

        Select(h.Grid, 2, 3);
        h.Grid.MergeCommand.Execute(null);
        Assert.Equal(4, h.Grid.Rows.Count);
    }

    [AvaloniaFact]
    public async Task TranslateSelectedRowsAsksOnceBeforeOverwriting()
    {
        using var project = new TestProject();
        var h = await Open(project);
        h.Grid.Rows[1].Target = "旧的";
        Select(h.Grid, 1, 2);

        await h.Grid.MachineTranslateCommand.ExecuteAsync(null);

        Assert.Equal(1, h.Dialogs.Confirmations);
        Assert.Equal("机翻结果。", h.Grid.Rows[1].Target);
        Assert.Equal("机翻结果。", h.Grid.Rows[2].Target);
        Assert.Equal(Loc.Format("MtDone", 2), h.Vm.Notice);
    }

    [AvaloniaFact]
    public async Task TranslateWholeFileReplacesEverything()
    {
        using var project = new TestProject();
        var h = await Open(project, "guide/setup.md");
        h.Grid.Rows[0].Target = "旧的";
        h.Grid.Rows[0].Confirm();
        h.Grid.OnlyNeedsReview = true;

        await h.Grid.MachineTranslateAllCommand.ExecuteAsync(null);

        Assert.False(h.Grid.OnlyNeedsReview);
        Assert.Equal(1, h.Dialogs.Confirmations);
        var texts = h.Grid.Rows.Where(r => !r.IsVerbatim).ToList();
        Assert.Equal(2, texts.Count);
        Assert.All(texts, r => Assert.Equal(("机翻结果。", SegmentStatus.Draft), (r.Target, r.Status)));
        Assert.Equal("```\ncode\n```", h.Grid.Rows.Single(r => r.IsVerbatim).Target);
    }

    [AvaloniaFact]
    public async Task DecliningWholeFileTranslationChangesNothing()
    {
        using var project = new TestProject();
        var h = await Open(project);
        h.Dialogs.ConfirmAnswer = false;

        await h.Grid.MachineTranslateAllCommand.ExecuteAsync(null);

        Assert.All(h.Grid.Rows, r => Assert.Equal("", r.Target));
    }

    sealed class StoppingLlm : ILlmClient
    {
        public SegmentGridViewModel? Grid { get; set; }
        public int Calls { get; private set; }

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Calls == 2)
                Grid!.StopTranslatingCommand.Execute(null);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("好。");
        }
    }

    [AvaloniaFact]
    public async Task StoppingKeepsWhatIsDone()
    {
        using var project = new TestProject();
        var llm = new StoppingLlm();
        var services = TestServices.Create();
        services = new AppServices
        {
            Settings = services.Settings,
            SaveSettings = services.SaveSettings,
            Memory = services.Memory,
            Credentials = services.Credentials,
            CreateLlmClient = (_, _) => llm,
        };
        var h = await Open(project, services: services);
        llm.Grid = h.Grid;

        await h.Grid.MachineTranslateAllCommand.ExecuteAsync(null);

        Assert.Equal(["好。", "", ""], h.Grid.Rows.Select(r => r.Target));
        Assert.Equal(Loc.Format("MtStopped", 1, 3), h.Vm.Notice);
        Assert.False(h.Grid.IsTranslating);
    }
}
