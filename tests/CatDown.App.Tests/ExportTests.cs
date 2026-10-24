using Avalonia.Headless.XUnit;
using CatDown.App.Localization;
using CatDown.App.ViewModels;
using CatDown.App.Views;
using CatDown.Core.Storage;

namespace CatDown.App.Tests;

public class ExportTests
{
    static async Task<(MainWindowViewModel Vm, FakeDialogs Dialogs)> Open(TestProject project)
    {
        Loc.Instance.SetLanguage("en");
        var dialogs = new FakeDialogs();
        var vm = new MainWindowViewModel(dialogs);
        new MainWindow { DataContext = vm }.Show();
        await vm.OpenProject(project.Path);
        return (vm, dialogs);
    }

    [AvaloniaFact]
    public async Task ExportAllMirrorsFolders()
    {
        using var project = new TestProject();
        var (vm, dialogs) = await Open(project);
        vm.OpenFile("README.md");
        vm.Editor!.Rows[0].Target = "你好";
        vm.Editor.Rows[0].Confirm();
        var output = Path.Combine(project.Path, "out");
        dialogs.FolderToPick = output;

        await vm.ExportAllCommand.ExecuteAsync(null);

        Assert.Equal("# 你好\n\nFirst sentence. Second sentence.\n", File.ReadAllText(Path.Combine(output, "README.md")));
        Assert.Equal(project.Read("guide/setup.md"), File.ReadAllText(Path.Combine(output, "guide", "setup.md")));
        Assert.Equal(project.Read("guide/api.md"), File.ReadAllText(Path.Combine(output, "guide", "api.md")));
        Assert.Equal("out", new ProjectStore(project.Path).LoadSettings()!.OutputFolder);
        Assert.DoesNotContain(vm.Files, f => f.Name == "out");
        var message = Assert.Single(dialogs.Messages).Message;
        Assert.Equal(Loc.Format("ExportDone", 3, 5), message);
    }

    [AvaloniaFact]
    public async Task ExportCurrentUsesSavedFolderWithoutAsking()
    {
        using var project = new TestProject();
        var (vm, dialogs) = await Open(project);
        vm.Settings!.OutputFolder = "../translated";
        vm.OpenFile("guide/api.md");
        dialogs.FolderToPick = null;

        await vm.ExportCurrentCommand.ExecuteAsync(null);

        var output = Path.GetFullPath(Path.Combine(project.Path, "..", "translated"));
        try
        {
            Assert.True(File.Exists(Path.Combine(output, "guide", "api.md")));
            Assert.False(File.Exists(Path.Combine(output, "README.md")));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task ProjectFolderItselfIsRefused()
    {
        using var project = new TestProject();
        var (vm, dialogs) = await Open(project);
        dialogs.FolderToPick = project.Path;

        await vm.ExportAllCommand.ExecuteAsync(null);

        Assert.Equal(Loc.Get("OutputIsProjectRoot"), Assert.Single(dialogs.Messages).Message);
        Assert.Null(vm.Settings!.OutputFolder);
        Assert.Equal("# Hello\n\nFirst sentence. Second sentence.\n", project.Read("README.md"));
    }

    [AvaloniaFact]
    public async Task FailedFileIsReportedAndOthersExport()
    {
        using var project = new TestProject();
        var (vm, dialogs) = await Open(project);
        var output = Path.Combine(project.Path, "out");
        var locked = Path.Combine(output, "README.md");
        Directory.CreateDirectory(output);
        File.WriteAllText(locked, "old");
        File.SetAttributes(locked, FileAttributes.ReadOnly);
        dialogs.FolderToPick = output;

        try
        {
            await vm.ExportAllCommand.ExecuteAsync(null);

            var message = Assert.Single(dialogs.Messages).Message;
            Assert.Contains(Loc.Format("ExportFailed", 1), message);
            Assert.Contains("README.md", message);
            Assert.True(File.Exists(Path.Combine(output, "guide", "api.md")));
            Assert.True(File.Exists(Path.Combine(output, "guide", "setup.md")));
        }
        finally
        {
            File.SetAttributes(locked, FileAttributes.Normal);
        }
    }
}
