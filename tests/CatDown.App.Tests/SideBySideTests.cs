using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CatDown.App.Localization;
using CatDown.App.ViewModels;
using CatDown.App.Views;
using CatDown.Core.Model;
using CatDown.Core.Storage;

namespace CatDown.App.Tests;

public class SideBySideTests
{
    static async Task<(MainWindow Window, MainWindowViewModel Vm, SideBySideView View)> Open(TestProject project)
    {
        Loc.Instance.SetLanguage("en");
        var vm = new MainWindowViewModel(new FakeDialogs(), TestServices.Create());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        await vm.OpenProject(project.Path);
        vm.OpenFile("README.md");
        vm.Editor!.ToggleLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var view = window.GetVisualDescendants().OfType<SideBySideView>().Single();
        return (window, vm, view);
    }

    [AvaloniaFact]
    public async Task ShowsOneSegmentPerLineAndRemembersLayout()
    {
        using var project = new TestProject();
        var (_, vm, view) = await Open(project);

        Assert.True(view.IsEffectivelyVisible);
        Assert.Equal("Hello\n\nFirst sentence.\nSecond sentence.", view.SourceEditor.Text);
        Assert.Equal("\n\n\n", view.TargetEditor.Text);
        Assert.Equal(EditorLayout.SideBySide, new ProjectStore(project.Path).LoadSettings()!.Layout);
        Assert.Equal("Segment grid", vm.Editor!.LayoutButtonText);
    }

    [AvaloniaFact]
    public async Task TypingUpdatesTheSegmentUnderTheCaret()
    {
        using var project = new TestProject();
        var (window, vm, view) = await Open(project);
        var grid = vm.Editor!;
        view.TargetEditor.TextArea.Focus();
        view.TargetEditor.CaretOffset = 2;
        Dispatcher.UIThread.RunJobs();
        Assert.Same(grid.Rows[1], grid.SelectedRow);

        window.KeyTextInput("第一句。");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("第一句。", grid.Rows[1].Target);
        Assert.Equal(SegmentStatus.Draft, grid.Rows[1].Status);
        Assert.Equal("", grid.Rows[0].Target);
        Assert.Equal("\n\n第一句。\n", view.TargetEditor.Text);
    }

    [AvaloniaFact]
    public async Task SeparatorsCannotBeDeleted()
    {
        using var project = new TestProject();
        var (window, _, view) = await Open(project);
        view.TargetEditor.TextArea.Focus();
        view.TargetEditor.CaretOffset = 2;
        window.KeyTextInput("x");

        for (var i = 0; i < 3; i++)
            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("\n\n\n", view.TargetEditor.Text);

        view.TargetEditor.CaretOffset = 2;
        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("\n\n\n", view.TargetEditor.Text);
    }

    [AvaloniaFact]
    public async Task EnterInsideSegmentStaysInIt()
    {
        using var project = new TestProject();
        var (window, vm, view) = await Open(project);
        view.TargetEditor.TextArea.Focus();
        view.TargetEditor.CaretOffset = 0;
        window.KeyTextInput("a");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyTextInput("b");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("a\nb", vm.Editor!.Rows[0].Target);
        Assert.Equal("", vm.Editor.Rows[1].Target);
    }

    [AvaloniaFact]
    public async Task ConfirmMovesCaretAndOutsideChangesShowUp()
    {
        using var project = new TestProject();
        var (window, vm, view) = await Open(project);
        var grid = vm.Editor!;
        view.TargetEditor.TextArea.Focus();
        view.TargetEditor.CaretOffset = 0;
        window.KeyTextInput("你好");

        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(SegmentStatus.Confirmed, grid.Rows[0].Status);
        Assert.Same(grid.Rows[1], grid.SelectedRow);
        Assert.Equal(3, view.TargetEditor.TextArea.Caret.Line);

        grid.CopySourceCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("你好\n\nFirst sentence.\n", view.TargetEditor.Text);
    }

    [AvaloniaFact]
    public async Task ClickingSourceSelectsSegmentAndSetsSplitPoint()
    {
        using var project = new TestProject();
        var (_, vm, view) = await Open(project);
        var grid = vm.Editor!;
        var secondStart = "Hello\n\nFirst sentence.\n".Length;

        view.SourceEditor.CaretOffset = secondStart + "Second".Length;
        Dispatcher.UIThread.RunJobs();

        Assert.Same(grid.Rows[2], grid.SelectedRow);
        Assert.Equal("Second".Length, grid.Rows[2].SourceCaret);

        grid.SplitCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Hello\n\nFirst sentence.\nSecond\nsentence.", view.SourceEditor.Text);
        Assert.Equal("\n\n\n\n", view.TargetEditor.Text);
    }

    [AvaloniaFact]
    public async Task SwitchingBackShowsTheGrid()
    {
        using var project = new TestProject();
        var (window, vm, view) = await Open(project);
        vm.Editor!.ToggleLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.False(view.IsEffectivelyVisible);
        Assert.True(window.GetVisualDescendants().OfType<Avalonia.Controls.ListBox>().Single(l => l.Name == "RowList").IsEffectivelyVisible);
        Assert.Equal(EditorLayout.Grid, new ProjectStore(project.Path).LoadSettings()!.Layout);
    }
}
