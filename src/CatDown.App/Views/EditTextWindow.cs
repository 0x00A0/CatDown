using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using CatDown.App.Editor;
using CatDown.App.Localization;

namespace CatDown.App.Views;

// Edits a block's source text. Closes with the new text, or null when cancelled.
public class EditTextWindow : Window
{
    public EditTextWindow(string title, string text)
    {
        Title = title;
        Width = 640;
        Height = 360;
        MinWidth = 400;
        MinHeight = 200;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var editor = new MarkdownEditor
        {
            Markdown = text,
            FocusOnAttach = true,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };
        var save = new Button { Content = Loc.Get("SaveSource"), Classes = { "Primary" } };
        save.Theme = this.FindResource("SolidButton") as Avalonia.Styling.ControlTheme;
        save.Click += (_, _) => Close(editor.Markdown);
        var cancel = new Button { Content = Loc.Get("Cancel") };
        cancel.Click += (_, _) => Close(null);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
            Children = { cancel, save },
        };
        DockPanel.SetDock(buttons, Dock.Bottom);
        Content = new DockPanel
        {
            Margin = new Thickness(16),
            Children =
            {
                buttons,
                new Border
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(4),
                    [!Border.BorderBrushProperty] = this.GetResourceObservable("SemiColorBorder").ToBinding(),
                    Child = editor,
                },
            },
        };
    }
}
