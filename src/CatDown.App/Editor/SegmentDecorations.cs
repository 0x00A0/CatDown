using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using CatDown.App.ViewModels;
using CatDown.Core.Model;

namespace CatDown.App.Editor;

// Tints the full width of the lines that hold the current segment.
public sealed class SegmentHighlighter(Control host) : IBackgroundRenderer
{
    public (int Start, int End)? Range { get; set; }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Range is not { } range || textView.Document is not { } document || !textView.VisualLinesValid)
            return;
        if (!host.TryFindResource("SemiColorPrimaryLight", host.ActualThemeVariant, out var resource) || resource is not IBrush brush)
            return;

        var first = document.GetLineByOffset(Math.Min(range.Start, document.TextLength)).LineNumber;
        var last = document.GetLineByOffset(Math.Min(range.End, document.TextLength)).LineNumber;
        foreach (var line in textView.VisualLines)
        {
            if (line.FirstDocumentLine.LineNumber > last || line.LastDocumentLine.LineNumber < first)
                continue;
            var top = line.VisualTop - textView.VerticalOffset;
            drawingContext.FillRectangle(brush, new Rect(0, top, textView.Bounds.Width, line.Height));
        }
    }
}

// Draws each segment's status glyph next to its first line.
public sealed class StatusMargin : AbstractMargin
{
    public IReadOnlyList<(int Offset, SegmentRowViewModel Row)> Segments { get; set; } = [];

    protected override Size MeasureOverride(Size availableSize) => new(20, 0);

    protected override void OnTextViewChanged(TextView? oldTextView, TextView? newTextView)
    {
        if (oldTextView is not null)
            oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
        base.OnTextViewChanged(oldTextView, newTextView);
        if (newTextView is not null)
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
    }

    void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        if (TextView is not { VisualLinesValid: true } textView || textView.Document is not { } document)
            return;
        foreach (var (offset, row) in Segments)
        {
            var line = document.GetLineByOffset(Math.Min(offset, document.TextLength));
            if (textView.GetVisualLine(line.LineNumber) is not { } visual)
                continue;
            var brush = Brush(row.Status);
            if (brush is null)
                continue;
            var text = new FormattedText(row.StatusGlyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, weight: FontWeight.Bold), 12, brush);
            context.DrawText(text, new Point(4, visual.VisualTop - textView.VerticalOffset + 2));
        }
    }

    IBrush? Brush(SegmentStatus status)
    {
        var key = status switch
        {
            SegmentStatus.Confirmed => "SemiColorSuccess",
            SegmentStatus.Draft => "SemiColorPrimary",
            SegmentStatus.NeedsReview => "SemiColorWarning",
            _ => "SemiColorText3",
        };
        return this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;
    }
}
