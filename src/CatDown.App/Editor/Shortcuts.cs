using Avalonia;
using Avalonia.Input;

namespace CatDown.App.Editor;

public static class Shortcuts
{
    // Ctrl on Windows, Cmd on macOS.
    public static KeyModifiers CommandModifier =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    public static bool IsCommand(KeyModifiers modifiers) => modifiers.HasFlag(CommandModifier);
}
