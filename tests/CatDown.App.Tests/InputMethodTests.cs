using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using CatDown.App.Editor;
using CatDown.App.Localization;
using CatDown.App.ViewModels;
using CatDown.App.Views;

namespace CatDown.App.Tests;

// What a Chinese input method does: show the pinyin being composed (preedit), then commit
// the chosen characters as text input. Neither step may disturb the document or the shortcuts.
public class InputMethodTests
{
    static async Task<(MainWindow Window, MainWindowViewModel Vm)> Open(TestProject project)
    {
        Loc.Instance.SetLanguage("en");
        var vm = new MainWindowViewModel(new FakeDialogs(), TestServices.Create());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        await vm.OpenProject(project.Path);
        vm.OpenFile("README.md");
        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }

    static TextInputMethodClient ClientOf(TextEditor editor)
    {
        var request = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
        editor.TextArea.RaiseEvent(request);
        return request.Client ?? throw new InvalidOperationException("The editor offers no input method client.");
    }

    static void Compose(MainWindow window, TextEditor editor, params string[] steps)
    {
        // AvaloniaEdit does not draw the composition inline, so the input method shows it in its
        // own window next to the caret. That needs a caret rectangle on the editor's text view.
        var client = ClientOf(editor);
        Assert.NotNull(client.TextViewVisual);
        Assert.True(client.CursorRectangle.Height > 0);
        foreach (var step in steps)
        {
            client.SetPreeditText(step);
            Dispatcher.UIThread.RunJobs();
        }
    }

    static void Commit(MainWindow window, TextEditor editor, string text)
    {
        ClientOf(editor).SetPreeditText(null);
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task ComposingInTheGridDoesNotChangeTheTranslation()
    {
        using var project = new TestProject();
        var (window, vm) = await Open(project);
        var editor = window.GetVisualDescendants().OfType<MarkdownEditor>().Single();
        editor.TextArea.Focus();

        Compose(window, editor, "n", "ni", "ni'h", "ni'hao");
        Assert.Equal("", editor.Text);
        Assert.Equal("", vm.Editor!.Rows[0].Target);

        Commit(window, editor, "你好");
        Assert.Equal("你好", vm.Editor.Rows[0].Target);

        Compose(window, editor, "shi", "shi'jie");
        Commit(window, editor, "世界");
        Assert.Equal("你好世界", vm.Editor.Rows[0].Target);
        Assert.Same(vm.Editor.Rows[0], vm.Editor.SelectedRow);
    }

    [AvaloniaFact]
    public async Task ChinesePunctuationAndSpaceCommitStayInTheSegment()
    {
        using var project = new TestProject();
        var (window, vm) = await Open(project);
        var editor = window.GetVisualDescendants().OfType<MarkdownEditor>().Single();
        editor.TextArea.Focus();

        Commit(window, editor, "你好");
        Commit(window, editor, "，");
        Commit(window, editor, "世界");
        Commit(window, editor, "！");
        window.KeyPress(Key.Enter, RawInputModifiers.Control, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("你好，世界！", vm.Editor!.Rows[0].Target);
        Assert.True(vm.Editor.Rows[0].IsConfirmed);
        Assert.Same(vm.Editor.Rows[1], vm.Editor.SelectedRow);
    }

    [AvaloniaFact]
    public async Task ComposingInSideBySideKeepsSegmentsApart()
    {
        using var project = new TestProject();
        var (window, vm) = await Open(project);
        vm.Editor!.ToggleLayoutCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var view = window.GetVisualDescendants().OfType<SideBySideView>().Single();
        var editor = view.TargetEditor;
        editor.TextArea.Focus();
        editor.CaretOffset = 2;
        Dispatcher.UIThread.RunJobs();

        Compose(window, editor, "di", "di'yi", "di'yi'ju");
        Assert.Equal("", vm.Editor.Rows[1].Target);
        Commit(window, editor, "第一句");
        Commit(window, editor, "。");

        Assert.Equal("第一句。", vm.Editor.Rows[1].Target);
        Assert.Equal("", vm.Editor.Rows[0].Target);
        Assert.Equal("", vm.Editor.Rows[2].Target);
        Assert.Equal("\n\n第一句。\n", editor.Text);
    }
}
