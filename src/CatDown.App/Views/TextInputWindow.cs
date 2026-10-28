using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CatDown.App.Localization;

namespace CatDown.App.Views;

// Asks for one line of text. Closes with the text, or null when cancelled.
public class TextInputWindow : Window
{
    public TextInputWindow(string title, string prompt, string initial)
    {
        Title = title;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var box = new TextBox { Text = initial };
        var ok = new Button { Content = Loc.Get("Ok"), IsDefault = true };
        ok.Click += (_, _) => Close(box.Text ?? "");
        var cancel = new Button { Content = Loc.Get("Cancel"), IsCancel = true };
        cancel.Click += (_, _) => Close(null);

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap },
                box,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancel, ok },
                },
            },
        };
        Opened += (_, _) =>
        {
            box.Focus(NavigationMethod.Tab);
            box.SelectAll();
        };
    }
}
