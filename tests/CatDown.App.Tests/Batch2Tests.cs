using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using CatDown.App.Localization;
using CatDown.App.Services;
using CatDown.App.ViewModels;
using CatDown.App.Views;
using CatDown.Core.Glossary;
using CatDown.Core.Llm;
using CatDown.Core.Model;
using CatDown.Core.Storage;

namespace CatDown.App.Tests;

public class Batch2Tests
{
    sealed record Harness(MainWindow Window, MainWindowViewModel Vm, FakeDialogs Dialogs, AppServices Services, FakeLlm Llm)
    {
        public SegmentGridViewModel Grid => Vm.Editor!;
    }

    static async Task<Harness> Open(TestProject project, string file = "README.md", Action<AppServices>? setup = null)
    {
        Loc.Instance.SetLanguage("en");
        var llm = new FakeLlm();
        var services = TestServices.Create(llm);
        setup?.Invoke(services);
        var dialogs = new FakeDialogs();
        var vm = new MainWindowViewModel(dialogs, services);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        await vm.OpenProject(project.Path);
        vm.OpenFile(file);
        Dispatcher.UIThread.RunJobs();
        return new Harness(window, vm, dialogs, services, llm);
    }

    [AvaloniaFact]
    public async Task ConfirmedSentencesFeedTheTranslationMemory()
    {
        using var project = new TestProject();
        project.Write("README.md", "Install the tool. Install the tools.\n");
        var h = await Open(project);

        h.Grid.Rows[0].Target = "安装工具。";
        h.Grid.ConfirmCommand.Execute(null);

        Assert.Same(h.Grid.Rows[1], h.Grid.SelectedRow);
        var match = Assert.Single(h.Grid.Matches);
        Assert.Equal("安装工具。", match.Target);
        Assert.EndsWith("%", match.Score);

        h.Window.KeyPress(Key.D1, RawInputModifiers.Control, PhysicalKey.Digit1, "1");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("安装工具。", h.Grid.Rows[1].Target);
        Assert.Equal(SegmentStatus.Draft, h.Grid.Rows[1].Status);
    }

    [AvaloniaFact]
    public async Task ExactMatchFromAnotherFile()
    {
        using var project = new TestProject();
        var h = await Open(project);
        h.Grid.SelectedRow = h.Grid.Rows[1];
        h.Grid.Rows[1].Target = "第一句。";
        h.Grid.ConfirmCommand.Execute(null);

        project.Write("guide/api.md", "First sentence.\n");
        h.Vm.OpenFile("guide/api.md");

        Assert.Equal("100%", Assert.Single(h.Vm.Editor!.Matches).Score);
    }

    [AvaloniaFact]
    public async Task CodeBlocksDoNotFeedTheMemory()
    {
        using var project = new TestProject();
        var h = await Open(project, "guide/setup.md");
        h.Grid.SelectedRow = h.Grid.Rows.Single(r => r.IsVerbatim);
        h.Grid.ConfirmCommand.Execute(null);
        Assert.Equal(0, h.Services.Memory.Count());
        Assert.Empty(h.Grid.Matches);
    }

    [AvaloniaFact]
    public async Task GlossaryTermsAreHighlightedAndListed()
    {
        using var project = new TestProject();
        new ProjectStore(project.Path).SaveGlossary(new Glossary
        {
            Entries = [new GlossaryEntry { Source = "sentence", Target = "句子", Note = "统一译法" }],
        });
        var h = await Open(project);

        var row = h.Grid.Rows[1];
        Assert.Equal([("First ", false), ("sentence", true), (".", false)], row.SourceParts.Select(p => (p.Text, p.Highlighted)));
        Assert.Equal("sentence → 句子 · 统一译法", row.GlossaryTip);
        h.Grid.SelectedRow = row;
        Assert.Equal(new GlossaryHitViewModel("sentence", "句子", "统一译法"), Assert.Single(h.Grid.Hits));
    }

    [AvaloniaFact]
    public async Task EditingGlossarySavesAndRehighlights()
    {
        using var project = new TestProject();
        var h = await Open(project);
        Assert.Empty(h.Grid.Rows[0].GlossaryHits);
        h.Dialogs.EditGlossary = g => new Glossary { Entries = [new GlossaryEntry { Source = "hello", Target = "你好" }] };

        await h.Vm.OpenGlossaryCommand.ExecuteAsync(null);

        Assert.Single(h.Grid.Rows[0].GlossaryHits);
        Assert.Equal("hello", new ProjectStore(project.Path).LoadGlossary().Entries.Single().Source);
    }

    [AvaloniaFact]
    public async Task CancelledGlossaryEditChangesNothing()
    {
        using var project = new TestProject();
        var h = await Open(project);
        await h.Vm.OpenGlossaryCommand.ExecuteAsync(null);
        Assert.False(File.Exists(Path.Combine(project.Path, ".catdown", "glossary.json")));
    }

    [Fact]
    public void GlossaryEditorDropsEmptyRows()
    {
        var vm = new GlossaryEditorViewModel(new Glossary { Entries = [new GlossaryEntry { Source = "a", Target = "b" }] });
        vm.AddCommand.Execute(null);
        vm.Entries[1].Source = "  ";
        vm.AddCommand.Execute(null);
        vm.Entries[2].Source = " c ";
        vm.Entries[2].Target = "d";
        vm.RemoveCommand.Execute(vm.Entries[0]);

        Assert.Equal([("c", "d")], vm.ToGlossary().Entries.Select(e => (e.Source, e.Target)));
    }

    [AvaloniaFact]
    public async Task MachineTranslationNeedsSettings()
    {
        using var project = new TestProject();
        var h = await Open(project);

        await h.Grid.MachineTranslateCommand.ExecuteAsync(null);

        Assert.Equal(Loc.Get("MtNotConfigured"), h.Vm.Notice);
        Assert.Empty(h.Llm.Calls);
    }

    static void Configure(AppServices services)
    {
        services.Settings.Llm.BaseUrl = "https://api.example.com/v1";
        services.Settings.Llm.Model = "test-model";
        services.Credentials.Write(LlmSettings.ApiKeyName, "stored-key");
    }

    [AvaloniaFact]
    public async Task MachineTranslationFillsDraftWithContext()
    {
        using var project = new TestProject();
        new ProjectStore(project.Path).SaveGlossary(new Glossary { Entries = [new GlossaryEntry { Source = "sentence", Target = "句子" }] });
        var h = await Open(project, setup: Configure);
        h.Grid.Rows[1].Target = "第一句。";
        h.Grid.ConfirmCommand.Execute(null);
        h.Grid.SelectedRow = h.Grid.Rows[2];
        h.Llm.Reply = "```\n第二句。\n```";

        h.Window.KeyPress(Key.M, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.M, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("第二句。", h.Grid.Rows[2].Target);
        Assert.Equal(SegmentStatus.Draft, h.Grid.Rows[2].Status);
        Assert.Equal("stored-key", TestServices.LastKey);
        var prompt = h.Llm.Calls.Single()[1].Content;
        Assert.Contains("First sentence. => 第一句。", prompt);
        Assert.Contains("sentence => 句子", prompt);
        Assert.EndsWith("Second sentence.", prompt);
    }

    [AvaloniaFact]
    public async Task MachineTranslationAsksBeforeOverwriting()
    {
        using var project = new TestProject();
        var h = await Open(project, setup: Configure);
        h.Grid.Rows[0].Target = "我自己的译文";
        h.Dialogs.ConfirmAnswer = false;

        await h.Grid.MachineTranslateCommand.ExecuteAsync(null);

        Assert.Equal(1, h.Dialogs.Confirmations);
        Assert.Equal("我自己的译文", h.Grid.Rows[0].Target);
        Assert.Empty(h.Llm.Calls);
    }

    [AvaloniaFact]
    public async Task MachineTranslationErrorShowsNotice()
    {
        using var project = new TestProject();
        var h = await Open(project, setup: Configure);
        h.Llm.Error = new LlmException("HTTP 401: bad key");

        await h.Grid.MachineTranslateCommand.ExecuteAsync(null);

        Assert.Equal(Loc.Format("MtFailed", "HTTP 401: bad key"), h.Vm.Notice);
        Assert.Equal("", h.Grid.Rows[0].Target);
        Assert.False(h.Grid.IsTranslating);
    }

    [AvaloniaFact]
    public void SettingsStoreKeyInCredentialStore()
    {
        Loc.Instance.SetLanguage("en");
        var services = TestServices.Create();
        var vm = new SettingsViewModel(services);
        Assert.Equal(Loc.Get("KeyMissing"), vm.KeyStatus);

        vm.BaseUrl = " http://localhost:11434/v1 ";
        vm.Model = "qwen";
        vm.ApiKeyInput = "secret-value";
        vm.SaveKeyCommand.Execute(null);

        Assert.Equal("http://localhost:11434/v1", services.Settings.Llm.BaseUrl);
        Assert.Equal("qwen", services.Settings.Llm.Model);
        Assert.Equal("secret-value", services.Credentials.Read(LlmSettings.ApiKeyName));
        Assert.Equal("", vm.ApiKeyInput);
        Assert.Equal(Loc.Get("KeySaved"), vm.KeyStatus);

        vm.ClearKeyCommand.Execute(null);
        Assert.Null(services.Credentials.Read(LlmSettings.ApiKeyName));
    }

    [AvaloniaFact]
    public void SettingsWithoutCredentialStore()
    {
        Loc.Instance.SetLanguage("en");
        var services = TestServices.Create();
        ((FakeCredentials)services.Credentials).IsAvailable = false;
        var vm = new SettingsViewModel(services);
        Assert.False(vm.CanStoreKeys);
        Assert.Equal(Loc.Get("KeyUnavailable"), vm.KeyStatus);
    }
}
