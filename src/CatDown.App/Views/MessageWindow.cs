using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CatDown.App.Localization;

namespace CatDown.App.Views;

public class MessageWindow : Window
{
    public MessageWindow(string title, string message)
    {
        Title = title;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var close = new Button { Content = Loc.Get("Ok"), HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children =
            {
                new SelectableTextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                close,
            },
        };
    }
}
