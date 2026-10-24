using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace CatDown.App.Controls;

public sealed record TextPart(string Text, bool Highlighted);

// Highlight.Parts="{Binding ...}" on a TextBlock: shows the parts as runs, highlighted ones tinted.
public sealed class Highlight
{
    public static readonly AttachedProperty<IReadOnlyList<TextPart>?> PartsProperty =
        AvaloniaProperty.RegisterAttached<Highlight, TextBlock, IReadOnlyList<TextPart>?>("Parts");

    static Highlight() => PartsProperty.Changed.AddClassHandler<TextBlock>((block, _) => Apply(block));

    Highlight() { }

    public static IReadOnlyList<TextPart>? GetParts(TextBlock block) => block.GetValue(PartsProperty);

    public static void SetParts(TextBlock block, IReadOnlyList<TextPart>? value) => block.SetValue(PartsProperty, value);

    static void Apply(TextBlock block)
    {
        block.Inlines ??= [];
        block.Inlines.Clear();
        foreach (var part in GetParts(block) ?? [])
        {
            var run = new Run(part.Text);
            if (part.Highlighted)
            {
                run.Bind(TextElement.BackgroundProperty, block.GetResourceObservable("SemiColorWarningLight"));
                run.TextDecorations = Avalonia.Media.TextDecorations.Underline;
            }
            block.Inlines.Add(run);
        }
    }
}
