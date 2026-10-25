using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CatDown.App.Localization;
using CatDown.App.Services;
using CatDown.App.ViewModels;
using CatDown.App.Views;
using CatDown.Core.Editing;
using CatDown.Core.Glossary;
using CatDown.Core.Model;
using CatDown.Core.Storage;

namespace CatDown.App.Tests;

public class Batch3Tests
{
    const string SemicolonSrx = """
        <srx version="2.0"><header cascade="no" /><body>
          <languagerules><languagerule languagerulename="Semi">
            <rule break="yes"><beforebreak>[;.]</beforebreak><afterbreak>\s</afterbreak></rule>
          </languagerule></languagerules>
          <maprules><languagemap languagepattern="en" languagerulename="Semi" /></maprules>
        </body></srx>
        """;

    sealed record Harness(MainWindow Window, MainWindowViewModel Vm, FakeDialogs Dialogs, AppServices Services, FakeLlm Llm);

    static async Task<Harness> Open(TestProject project, bool configureLlm = false)
    {
        Loc.Instance.SetLanguage("en");
        var llm = new FakeLlm();
        var services = TestServices.Create(llm);
        if (configureLlm)
        {
            services.Settings.Llm.BaseUrl = "https://x";
            services.Settings.Llm.Model = "m";
        }
        var dialogs = new FakeDialogs();
        var vm = new MainWindowViewModel(dialogs, services);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        await vm.OpenProject(project.Path);
        vm.OpenFile("README.md");
        Dispatcher.UIThread.RunJobs();
        return new Harness(window, vm, dialogs, services, llm);
    }

    [AvaloniaFact]
    public async Task ToolbarMenusAreVisible()
    {
        using var project = new TestProject();
        var h = await Open(project);
        var buttons = h.Window.GetVisualDescendants().OfType<DropDownButton>().ToList();
        Assert.Equal(3, buttons.Count);
        Assert.All(buttons, b => Assert.True(b.IsEffectivelyVisible && b.Bounds.Width > 0, b.Name));
    }

    [AvaloniaFact]
    public async Task ImportSrxAndResegmentTranslatedFile()
    {
        using var project = new TestProject();
        project.Write("README.md", "Install it; then run it.\n");
        var srx = project.Write("rules.srx", SemicolonSrx);
        var h = await Open(project);
        var grid = h.Vm.Editor!;
        grid.Rows[0].Target = "安装并运行。";
        grid.ConfirmCommand.Execute(null);

        h.Dialogs.FileToPick = srx;
        await h.Vm.ImportSrxCommand.ExecuteAsync(null);
        Assert.Equal(Loc.Get("SrxImported"), h.Vm.Notice);
        Assert.True(new ProjectStore(project.Path).HasSrx);

        h.Dialogs.ResegmentChoices.Enqueue(new ResegmentChoice(TranslatedBlockAction.MergeIntoFirst, false));
        await h.Vm.ResegmentFileCommand.ExecuteAsync(null);

        Assert.Equal(["Install it; then run it."], h.Dialogs.AskedParagraphs);
        Assert.Equal(
            [("Install it;", "安装并运行。", SegmentStatus.NeedsReview), ("then run it.", "", SegmentStatus.NeedsReview)],
            h.Vm.Editor!.Rows.Select(r => (r.Source, r.Target, r.Status)).ToList());
        Assert.Equal(Loc.Format("ResegmentDone", 1, 0), h.Vm.Notice);

        h.Vm.RemoveSrxCommand.Execute(null);
        Assert.False(new ProjectStore(project.Path).HasSrx);
    }

    [AvaloniaFact]
    public async Task ApplyToAllAsksOnce()
    {
        using var project = new TestProject();
        project.Write("README.md", "One; two.\n\nThree; four.\n");
        var h = await Open(project);
        foreach (var row in h.Vm.Editor!.Rows)
        {
            row.Target = "译";
            row.Confirm();
        }
        new ProjectStore(project.Path).ImportSrx(project.Write("rules.srx", SemicolonSrx));
        h.Dialogs.ResegmentChoices.Enqueue(new ResegmentChoice(TranslatedBlockAction.Skip, true));

        await h.Vm.ResegmentFileCommand.ExecuteAsync(null);

        Assert.Single(h.Dialogs.AskedParagraphs);
        Assert.Equal(2, h.Vm.Editor!.Rows.Count);
        Assert.Equal(Loc.Format("ResegmentDone", 0, 2), h.Vm.Notice);
    }

    [AvaloniaFact]
    public async Task BrokenSrxShowsMessage()
    {
        using var project = new TestProject();
        var h = await Open(project);
        h.Dialogs.FileToPick = project.Write("bad.srx", "<srx>");

        await h.Vm.ImportSrxCommand.ExecuteAsync(null);

        Assert.StartsWith(Loc.Format("SrxInvalid", "").TrimEnd(), Assert.Single(h.Dialogs.Messages).Message);
        Assert.False(new ProjectStore(project.Path).HasSrx);
    }

    [AvaloniaFact]
    public async Task LlmSegmentationOfOneParagraph()
    {
        using var project = new TestProject();
        project.Write("README.md", "Install it, then run it.\n\nOther text.\n");
        var h = await Open(project, configureLlm: true);
        h.Llm.Reply = """["Install it,", "then run it."]""";

        await h.Vm.Editor!.LlmSegmentBlockCommand.ExecuteAsync(null);

        Assert.Equal(["Install it,", "then run it.", "Other text."], h.Vm.Editor!.Rows.Select(r => r.Source));
        Assert.Single(h.Llm.Calls);
    }

    [AvaloniaFact]
    public async Task LlmSegmentationFailureIsReported()
    {
        using var project = new TestProject();
        var h = await Open(project, configureLlm: true);
        h.Llm.Reply = """["Something else entirely."]""";
        h.Vm.Editor!.SelectedRow = h.Vm.Editor.Rows[1];

        await h.Vm.Editor.LlmSegmentBlockCommand.ExecuteAsync(null);

        Assert.StartsWith(Loc.Format("LlmSegmentFailed", "").TrimEnd(), h.Vm.Notice);
        Assert.Equal(3, h.Vm.Editor.Rows.Count);
    }

    [AvaloniaFact]
    public async Task LlmSegmentationNeedsSettings()
    {
        using var project = new TestProject();
        var h = await Open(project);
        await h.Vm.Editor!.LlmSegmentFileCommand.ExecuteAsync(null);
        Assert.Equal(Loc.Get("MtNotConfigured"), h.Vm.Notice);
    }

    [AvaloniaFact]
    public async Task TmxExportAndImportFromSettings()
    {
        using var project = new TestProject();
        var services = TestServices.Create();
        services.Memory.Add("en", "zh", "Hello.", "你好。");
        var dialogs = new FakeDialogs { SavePath = Path.Combine(project.Path, "out.tmx") };
        var vm = new SettingsViewModel(services, dialogs);
        Assert.Equal(Loc.Format("TmCount", 1), vm.TmCount);

        await vm.ExportTmxCommand.ExecuteAsync(null);
        Assert.True(File.Exists(dialogs.SavePath));

        var other = TestServices.Create();
        dialogs.FileToPick = dialogs.SavePath;
        var otherVm = new SettingsViewModel(other, dialogs);
        await otherVm.ImportTmxCommand.ExecuteAsync(null);

        Assert.Equal(Loc.Format("TmxImported", 1), otherVm.TmStatus);
        Assert.Equal(Loc.Format("TmCount", 1), otherVm.TmCount);
    }

    [AvaloniaFact]
    public async Task GlossaryCsvImportAndExport()
    {
        using var project = new TestProject();
        var dialogs = new FakeDialogs
        {
            FileToPick = project.Write("terms.csv", "source,target,note\nAPI,接口,\nCLI,CLI,不翻\n"),
            SavePath = Path.Combine(project.Path, "out.csv"),
        };
        var vm = new GlossaryEditorViewModel(new Glossary { Entries = [new GlossaryEntry { Source = "API", Target = "接口" }] }, dialogs);

        await vm.ImportCsvCommand.ExecuteAsync(null);
        Assert.Equal(["API", "CLI"], vm.Entries.Select(e => e.Source));
        Assert.Equal(Loc.Format("CsvImported", 1), vm.Status);

        await vm.ExportCsvCommand.ExecuteAsync(null);
        Assert.Contains("CLI,CLI,不翻", File.ReadAllText(dialogs.SavePath));
    }
}
