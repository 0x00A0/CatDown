using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CatDown.App.Editor;
using CatDown.App.Localization;
using CatDown.App.ViewModels;
using CatDown.App.Views;
using CatDown.Core.Model;
using CatDown.Core.Storage;

namespace CatDown.App.Tests;

public class SegmentGridTests
{
    static async Task<(MainWindow Window, MainWindowViewModel Vm)> OpenFile(TestProject project, string file)
    {
        Loc.Instance.SetLanguage("en");
        var vm = new MainWindowViewModel(new FakeDialogs());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        await vm.OpenProject(project.Path);
        vm.OpenFile(file);
        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }

    static MarkdownEditor? SelectedEditor(Window window) =>
        window.GetVisualDescendants().OfType<MarkdownEditor>().FirstOrDefault();

    [AvaloniaFact]
    public async Task ShowsOneRowPerSegment()
    {
        using var project = new TestProject();
        var (window, vm) = await OpenFile(project, "README.md");
        var grid = vm.Editor!;

        Assert.Equal(vm.Session!.Document.AllSegments.Count(), grid.Rows.Count);
        Assert.Equal(["Hello", "First sentence.", "Second sentence."], grid.Rows.Select(r => r.Source));
        Assert.Same(grid.Rows[0], grid.SelectedRow);
        Assert.NotNull(SelectedEditor(window));
    }

    [AvaloniaFact]
    public async Task TypingAndConfirmingWithShortcut()
    {
        using var project = new TestProject();
        var (window, vm) = await OpenFile(project, "README.md");
        var grid = vm.Editor!;
        var editor = SelectedEditor(window)!;
        editor.TextArea.Focus();

        window.KeyTextInput("你好");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("你好", grid.Rows[0].Target);
        Assert.Equal(SegmentStatus.Draft, grid.Rows[0].Status);

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(SegmentStatus.Confirmed, grid.Rows[0].Status);
        Assert.Same(grid.Rows[1], grid.SelectedRow);
        Assert.True(grid.HasPendingSave);

        grid.SaveNow();
        var saved = new ProjectStore(project.Path).LoadFile("README.md").Document!;
        Assert.Equal("你好", saved.AllSegments.First().Target);
        Assert.Equal(33, vm.Files.Single(f => f.Name == "README.md").Progress);
    }

    [AvaloniaFact]
    public async Task ConfirmNeedsText()
    {
        using var project = new TestProject();
        var (_, vm) = await OpenFile(project, "README.md");
        var grid = vm.Editor!;

        grid.ConfirmCommand.Execute(null);

        Assert.Equal(SegmentStatus.Untranslated, grid.Rows[0].Status);
    }

    [AvaloniaFact]
    public async Task CopySourceFillsTarget()
    {
        using var project = new TestProject();
        var (_, vm) = await OpenFile(project, "README.md");
        var grid = vm.Editor!;
        grid.SelectedRow = grid.Rows[1];

        grid.CopySourceCommand.Execute(null);

        Assert.Equal("First sentence.", grid.Rows[1].Target);
    }

    [AvaloniaFact]
    public async Task MergeAndSplit()
    {
        using var project = new TestProject();
        var (_, vm) = await OpenFile(project, "README.md");
        var grid = vm.Editor!;
        grid.SelectedRow = grid.Rows[1];

        grid.MergeCommand.Execute(null);
        Assert.Equal(["Hello", "First sentence. Second sentence."], grid.Rows.Select(r => r.Source));
        Assert.Equal("First sentence. Second sentence.", grid.SelectedRow!.Source);

        grid.SelectedRow.SourceCaret = "First sentence.".Length;
        grid.SplitCommand.Execute(null);
        Assert.Equal(["Hello", "First sentence.", "Second sentence."], grid.Rows.Select(r => r.Source));

        var saved = new ProjectStore(project.Path).LoadFile("README.md").Document!;
        Assert.Equal(3, saved.AllSegments.Count());
    }

    [AvaloniaFact]
    public async Task MergingLastSentenceShowsNotice()
    {
        using var project = new TestProject();
        var (_, vm) = await OpenFile(project, "README.md");
        vm.Editor!.SelectedRow = vm.Editor.Rows[0];

        vm.Editor.MergeCommand.Execute(null);

        Assert.Equal(Loc.Get("CannotMergeLast"), vm.Notice);
    }

    [AvaloniaFact]
    public async Task FormatShortcutWrapsSelection()
    {
        using var project = new TestProject();
        var (window, _) = await OpenFile(project, "README.md");
        var editor = SelectedEditor(window)!;
        editor.TextArea.Focus();
        window.KeyTextInput("word");
        editor.SelectAll();

        window.KeyPress(Key.B, RawInputModifiers.Control, PhysicalKey.B, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("**word**", editor.Text);
    }

    [Theory]
    [InlineData(InlineFormat.Bold, "a **bc** d", 4, 2)]
    [InlineData(InlineFormat.Italic, "a *bc* d", 3, 2)]
    [InlineData(InlineFormat.Code, "a `bc` d", 3, 2)]
    [InlineData(InlineFormat.Link, "a [bc](url) d", 7, 3)]
    public void FormattingWrapsSelection(InlineFormat format, string text, int start, int length)
    {
        var result = MarkdownFormatting.Apply("a bc d", 2, 2, format);
        Assert.Equal(new FormatResult(text, start, length), result);
    }

    [AvaloniaFact]
    public async Task OnlyNeedsReviewFilter()
    {
        using var project = new TestProject();
        var (_, vm) = await OpenFile(project, "README.md");
        var grid = vm.Editor!;
        grid.Rows[2].Segment.Status = SegmentStatus.NeedsReview;

        grid.OnlyNeedsReview = true;

        Assert.Equal(["Second sentence."], grid.Rows.Select(r => r.Source));
        Assert.Equal(3, grid.Rows[0].Number);
    }
}
