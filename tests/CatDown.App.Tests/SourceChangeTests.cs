using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CatDown.App.Localization;
using CatDown.App.ViewModels;
using CatDown.App.Views;
using CatDown.Core.Model;

namespace CatDown.App.Tests;

public class SourceChangeTests
{
    static async Task<(MainWindowViewModel Vm, FakeDialogs Dialogs)> OpenReadme(TestProject project)
    {
        Loc.Instance.SetLanguage("en");
        var dialogs = new FakeDialogs();
        var vm = new MainWindowViewModel(dialogs);
        new MainWindow { DataContext = vm }.Show();
        await vm.OpenProject(project.Path);
        vm.OpenFile("README.md");
        foreach (var row in vm.Editor!.Rows)
        {
            row.Target = row.Source + "-zh";
            row.Confirm();
        }
        vm.Editor.SaveNow();
        Dispatcher.UIThread.RunJobs();
        return (vm, dialogs);
    }

    [AvaloniaFact]
    public async Task OutsideChangeIsPickedUpWhenWindowActivates()
    {
        using var project = new TestProject();
        var (vm, _) = await OpenReadme(project);

        vm.OnActivated();
        Assert.Null(vm.Notice);

        project.Write("README.md", "# Hello\n\nFirst sentence. Second sentence changed.\n\nNew paragraph.\n");
        vm.OnActivated();

        Assert.Equal(Loc.Format("SourceChanged", 1, 1, 0), vm.Notice);
        var rows = vm.Editor!.Rows;
        Assert.Equal(SegmentStatus.NeedsReview, rows.Single(r => r.Source == "Second sentence changed.").Status);
        Assert.Equal(SegmentStatus.Untranslated, rows.Single(r => r.Source == "New paragraph.").Status);

        vm.Editor.OnlyNeedsReview = true;
        Assert.Equal(["Second sentence changed."], vm.Editor.Rows.Select(r => r.Source));
    }

    [AvaloniaFact]
    public async Task EditingSourceWritesFile()
    {
        using var project = new TestProject();
        var (vm, dialogs) = await OpenReadme(project);
        dialogs.EditText = text => text.Replace("First", "Very first");
        vm.Editor!.SelectedRow = vm.Editor.Rows[1];

        await vm.Editor.EditSourceCommand.ExecuteAsync(null);

        Assert.Equal("# Hello\n\nVery first sentence. Second sentence.\n", project.Read("README.md"));
        Assert.Equal(SegmentStatus.NeedsReview, vm.Editor.Rows[1].Status);
        Assert.Equal("First sentence.-zh", vm.Editor.Rows[1].Target);
        Assert.Equal(SegmentStatus.Confirmed, vm.Editor.Rows[2].Status);
        Assert.NotNull(vm.Notice);
    }

    [AvaloniaFact]
    public async Task CancelledSourceEditChangesNothing()
    {
        using var project = new TestProject();
        var (vm, _) = await OpenReadme(project);
        var before = project.Read("README.md");

        await vm.Editor!.EditSourceCommand.ExecuteAsync(null);

        Assert.Equal(before, project.Read("README.md"));
    }

    [AvaloniaFact]
    public async Task ReopeningFileAfterOutsideChangeShowsNotice()
    {
        using var project = new TestProject();
        var (vm, _) = await OpenReadme(project);
        vm.OpenFile("guide/api.md");

        project.Write("README.md", "# Hello\n\nFirst sentence.\n");
        vm.OpenFile("README.md");

        Assert.Equal(Loc.Format("SourceChanged", 0, 0, 1), vm.Notice);
    }

    [AvaloniaFact]
    public async Task DeletedSourceClosesFile()
    {
        using var project = new TestProject();
        var (vm, _) = await OpenReadme(project);

        File.Delete(Path.Combine(project.Path, "README.md"));
        vm.OnActivated();

        Assert.False(vm.HasFile);
        Assert.DoesNotContain(vm.Files, f => f.Name == "README.md");
        Assert.Equal(Loc.Format("SourceMissing", "README.md"), vm.Notice);
        Assert.True(File.Exists(Path.Combine(project.Path, ".catdown", "files", "README.md.json")));
    }
}
