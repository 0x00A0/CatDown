using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CatDown.App.Localization;
using CatDown.App.ViewModels;
using CatDown.App.Views;

namespace CatDown.App.Tests;

public class MainWindowTests
{
    static (MainWindow Window, MainWindowViewModel Vm, FakeDialogs Dialogs) Open()
    {
        Loc.Instance.SetLanguage("en");
        var dialogs = new FakeDialogs();
        var vm = new MainWindowViewModel(dialogs, TestServices.Create());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        return (window, vm, dialogs);
    }

    [AvaloniaFact]
    public void StartsWithoutProject()
    {
        var (window, _, _) = Open();
        Assert.True(window.IsVisible);
        Assert.Equal("CatDown", window.Title);
        Assert.True(window.FindControl<TextBlock>("Placeholder")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task OpeningNewFolderAsksLanguagesAndListsFiles()
    {
        using var project = new TestProject();
        var (window, vm, dialogs) = Open();
        dialogs.FolderToPick = project.Path;

        await vm.OpenFolderCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, dialogs.LanguageQuestions);
        Assert.True(File.Exists(Path.Combine(project.Path, ".catdown", "project.json")));
        Assert.Equal(["guide", "README.md"], vm.Files.Select(f => f.Name));
        Assert.Equal(["api.md", "setup.md"], vm.Files[0].Children.Select(f => f.Name));
        Assert.DoesNotContain(vm.Files, f => f.Name == ".catdown");
        Assert.Equal("EN → ZH", window.FindControl<TextBlock>("LanguagePair")!.Text);
        Assert.False(window.FindControl<TextBlock>("Placeholder")!.IsVisible);
    }

    [AvaloniaFact]
    public async Task ReopeningProjectDoesNotAskAgain()
    {
        using var project = new TestProject();
        var (_, vm, dialogs) = Open();
        await vm.OpenProject(project.Path);
        await vm.OpenProject(project.Path);
        Assert.Equal(1, dialogs.LanguageQuestions);
    }

    [AvaloniaFact]
    public async Task CancellingLanguagesDoesNotOpenProject()
    {
        using var project = new TestProject();
        var (_, vm, dialogs) = Open();
        dialogs.Languages = null;

        Assert.False(await vm.OpenProject(project.Path));
        Assert.False(vm.HasProject);
        Assert.False(File.Exists(Path.Combine(project.Path, ".catdown", "project.json")));
    }

    [AvaloniaFact]
    public async Task SelectingFileOpensItAndShowsProgress()
    {
        using var project = new TestProject();
        var (_, vm, _) = Open();
        await vm.OpenProject(project.Path);
        var readme = vm.Files.Single(f => f.Name == "README.md");
        Assert.Null(readme.Progress);

        vm.SelectedNode = readme;

        Assert.True(vm.HasFile);
        Assert.Equal("README.md", vm.Session!.RelativePath);
        Assert.Equal(0, readme.Progress);
        Assert.True(File.Exists(Path.Combine(project.Path, ".catdown", "files", "README.md.json")));
    }

    [Fact]
    public void NewProjectDialogRejectsSameLanguage()
    {
        Loc.Instance.SetLanguage("en");
        var vm = new NewProjectViewModel();
        Assert.True(vm.IsValid);
        vm.Target = vm.Languages.First(c => c.Value == "en");
        Assert.False(vm.IsValid);
        Assert.NotNull(vm.Error);
    }
}
