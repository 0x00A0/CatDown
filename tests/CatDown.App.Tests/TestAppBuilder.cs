using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(CatDown.App.Tests.TestAppBuilder))]

namespace CatDown.App.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
