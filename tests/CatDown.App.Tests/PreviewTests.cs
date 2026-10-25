using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CatDown.App.Localization;
using CatDown.App.Preview;
using CatDown.App.ViewModels;
using CatDown.App.Views;

namespace CatDown.App.Tests;

public class PreviewTests
{
    [AvaloniaFact]
    public async Task PreviewShowsTranslatedDocument()
    {
        using var project = new TestProject();
        Loc.Instance.SetLanguage("en");
        var vm = new MainWindowViewModel(new FakeDialogs(), TestServices.Create());
        var window = new MainWindow { DataContext = vm };
        window.Show();
        await vm.OpenProject(project.Path);
        vm.OpenFile("README.md");
        var grid = vm.Editor!;
        grid.Rows[0].Target = "你好";
        Dispatcher.UIThread.RunJobs();

        grid.TogglePreviewCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var list = window.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "RowList");
        var preview = window.GetVisualDescendants().OfType<MarkdownView>().Single();
        Assert.False(list.IsEffectivelyVisible);
        Assert.True(preview.IsEffectivelyVisible);
        Assert.Equal("Back to editing", grid.PreviewButtonText);
        var texts = preview.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Inlines?.Text ?? t.Text).ToList();
        Assert.Contains("你好", texts);
        Assert.Contains("First sentence. Second sentence.", texts);

        grid.TogglePreviewCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(list.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void RendersCommonBlocks()
    {
        var view = new MarkdownView
        {
            Markdown = "# Title\n\nText with **bold**, *italic*, `code` and [a link](https://x.io).\n\n- one\n- [x] done\n\n1. first\n\n> quote\n\n```\ncode block\n```\n\n| A | B |\n| --- | --- |\n| 1 | 2 |\n\n***\n",
        };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var texts = view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Inlines?.Text ?? t.Text).ToList();
        Assert.Contains("Title", texts);
        Assert.Contains("Text with bold, italic, code and a link.", texts);
        Assert.Contains("•", texts);
        Assert.Contains("☑", texts);
        Assert.Contains("1.", texts);
        Assert.Contains("quote", texts);
        Assert.Contains("code block", texts);
        Assert.Contains("B", texts);
        Assert.Contains("2", texts);
    }
}
