using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CatDown.App.Localization;
using CatDown.App.Services;
using CatDown.App.ViewModels;
using CatDown.App.Views;
using CatDown.Core.Llm;
using CatDown.Core.Model;

namespace CatDown.App.Tests;

public class ClaudeSettingsTests
{
    [AvaloniaFact]
    public void ChoosingClaudeShowsItsModelAndUsesItsOwnKey()
    {
        Loc.Instance.SetLanguage("en");
        var services = TestServices.Create();
        services.Credentials.Write(LlmSettings.ApiKeyName, "openai-key");
        var vm = new SettingsViewModel(services);
        var window = new SettingsWindow { DataContext = vm };
        window.Show();
        Assert.True(window.FindControl<TextBox>("BaseUrlBox")!.IsEffectivelyVisible);
        Assert.False(window.FindControl<AutoCompleteBox>("ClaudeModelBox")!.IsEffectivelyVisible);

        vm.SelectedProvider = vm.Providers.Single(p => p.Value == LlmProvider.Claude);
        Dispatcher.UIThread.RunJobs();

        Assert.False(window.FindControl<TextBox>("BaseUrlBox")!.IsEffectivelyVisible);
        Assert.True(window.FindControl<AutoCompleteBox>("ClaudeModelBox")!.IsEffectivelyVisible);
        Assert.Equal("claude-opus-5-5", vm.ClaudeModel);
        Assert.Equal(LlmProvider.Claude, services.Settings.Llm.Provider);
        Assert.Equal(Loc.Get("ClaudeKeyMissing"), vm.KeyStatus);

        vm.ApiKeyInput = "claude-key";
        vm.SaveKeyCommand.Execute(null);
        Assert.Equal("claude-key", services.Credentials.Read(LlmSettings.ClaudeApiKeyName));
        Assert.Equal("openai-key", services.Credentials.Read(LlmSettings.ApiKeyName));

        vm.ClaudeModel = " claude-sonnet-5-5 ";
        Assert.Equal("claude-sonnet-5-5", services.Settings.Llm.ClaudeModel);
    }

    [AvaloniaFact]
    public void ClaudeNeedsAKeyBeforeTranslating()
    {
        var services = TestServices.Create();
        services.Settings.Llm.Provider = LlmProvider.Claude;

        Assert.Null(services.LlmClient());

        services.Credentials.Write(LlmSettings.ClaudeApiKeyName, "claude-key");
        Assert.NotNull(services.LlmClient());
        Assert.Equal("claude-key", TestServices.LastKey);
    }

    [AvaloniaFact]
    public void DefaultFactoryBuildsTheRightClient()
    {
        var services = new AppServices
        {
            Settings = new AppSettings(),
            SaveSettings = _ => { },
            Memory = TestServices.Create().Memory,
            Credentials = new FakeCredentials(),
        };
        services.Settings.Llm.Model = "m";
        services.Credentials.Write(LlmSettings.ApiKeyName, "k");
        Assert.IsType<OpenAiCompatibleClient>(services.LlmClient());

        services.Settings.Llm.Provider = LlmProvider.Claude;
        services.Credentials.Write(LlmSettings.ClaudeApiKeyName, "k");
        Assert.IsType<ClaudeClient>(services.LlmClient());
    }
}
