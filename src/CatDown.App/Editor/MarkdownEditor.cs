using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;

namespace CatDown.App.Editor;

// AvaloniaEdit with a bindable Markdown text, Semi colors, and inline-format shortcuts.
public class MarkdownEditor : TextEditor
{
    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownEditor, string>(nameof(Markdown), "", defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> FocusOnAttachProperty =
        AvaloniaProperty.Register<MarkdownEditor, bool>(nameof(FocusOnAttach));

    bool _syncing;

    public MarkdownEditor()
    {
        WordWrap = true;
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        EditorTheme.Apply(this);
        TextChanged += (_, _) =>
        {
            if (_syncing)
                return;
            _syncing = true;
            SetCurrentValue(MarkdownProperty, Text);
            _syncing = false;
        };
        AddHandler(KeyDownEvent, OnFormatKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    protected override Type StyleKeyOverride => typeof(TextEditor);

    public string Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public bool FocusOnAttach
    {
        get => GetValue(FocusOnAttachProperty);
        set => SetValue(FocusOnAttachProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkdownProperty && !_syncing && Text != Markdown)
        {
            _syncing = true;
            Text = Markdown ?? "";
            _syncing = false;
        }
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (FocusOnAttach)
        {
            Dispatcher.UIThread.Post(() =>
            {
                TextArea.Focus();
                CaretOffset = Text.Length;
            });
        }
    }

    void OnFormatKey(object? sender, KeyEventArgs e)
    {
        if (!Shortcuts.IsCommand(e.KeyModifiers) || e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            return;
        InlineFormat? format = e.Key switch
        {
            Key.B => InlineFormat.Bold,
            Key.I => InlineFormat.Italic,
            Key.E => InlineFormat.Code,
            Key.K => InlineFormat.Link,
            _ => null,
        };
        if (format is null)
            return;

        var result = MarkdownFormatting.Apply(Text, SelectionStart, SelectionLength, format.Value);
        Document.Text = result.Text;
        Select(result.SelectionStart, result.SelectionLength);
        e.Handled = true;
    }
}
