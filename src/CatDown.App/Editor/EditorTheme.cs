using System.Xml;
using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace CatDown.App.Editor;

// Connects AvaloniaEdit to the Semi theme: editor colors and Markdown highlighting
// follow the light/dark switch.
public static class EditorTheme
{
    static IHighlightingDefinition? _highlighting;

    static readonly (string Name, string Resource)[] HighlightColors =
    [
        ("Heading", "SemiColorPrimary"),
        ("Strong", "SemiColorText0"),
        ("Emphasis", "SemiColorText0"),
        ("Code", "SemiColorDanger"),
        ("Link", "SemiColorLink"),
        ("Marker", "SemiColorText2"),
    ];

    public static IHighlightingDefinition Highlighting
    {
        get
        {
            if (_highlighting is null)
            {
                using var stream = typeof(EditorTheme).Assembly.GetManifestResourceStream("CatDown.App.Editor.MarkdownHighlighting.xshd")!;
                using var reader = XmlReader.Create(stream);
                _highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                UpdateColors();
                if (Application.Current is { } app)
                    app.ActualThemeVariantChanged += (_, _) => UpdateColors();
            }
            return _highlighting;
        }
    }

    static void UpdateColors()
    {
        if (_highlighting is null || Application.Current is not { } app)
            return;
        foreach (var (name, resource) in HighlightColors)
        {
            if (app.TryGetResource(resource, app.ActualThemeVariant, out var value) && value is ISolidColorBrush brush)
                _highlighting.GetNamedColor(name).Foreground = new SimpleHighlightingBrush(brush.Color);
        }
    }

    public static void Apply(TextEditor editor)
    {
        editor[!TextEditor.BackgroundProperty] = new DynamicResourceExtension("SemiColorBackground0");
        editor[!TextEditor.ForegroundProperty] = new DynamicResourceExtension("SemiColorText0");
        editor[!TextEditor.LineNumbersForegroundProperty] = new DynamicResourceExtension("SemiColorText3");
        editor.TextArea[!TextArea.SelectionBrushProperty] = new DynamicResourceExtension("SemiColorPrimaryLight");
        editor.TextArea[!TextArea.SelectionForegroundProperty] = new DynamicResourceExtension("SemiColorText0");
        editor.TextArea[!TextArea.CaretBrushProperty] = new DynamicResourceExtension("SemiColorPrimary");
        editor.SyntaxHighlighting = Highlighting;

        // Highlighting colors change in place; repaint when the theme flips.
        if (Application.Current is { } app)
        {
            void Redraw(object? sender, EventArgs e) => editor.TextArea.TextView.Redraw();
            editor.AttachedToVisualTree += (_, _) => app.ActualThemeVariantChanged += Redraw;
            editor.DetachedFromVisualTree += (_, _) => app.ActualThemeVariantChanged -= Redraw;
        }
    }
}
