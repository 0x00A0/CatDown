using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CatDown.App.Localization;

namespace CatDown.App.Views;

// A short message; with askToConfirm it closes with true (OK) or false (Cancel).
public class MessageWindow : Window
{
    public MessageWindow(string title, string message, bool askToConfirm = false)
    {
        Title = title;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var ok = new Button { Content = Loc.Get("Ok") };
        ok.Click += (_, _) => Close(true);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        if (askToConfirm)
        {
            var cancel = new Button { Content = Loc.Get("Cancel") };
            cancel.Click += (_, _) => Close(false);
            buttons.Children.Add(cancel);
        }
        buttons.Children.Add(ok);

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children =
            {
                new SelectableTextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                buttons,
            },
        };
    }
}
