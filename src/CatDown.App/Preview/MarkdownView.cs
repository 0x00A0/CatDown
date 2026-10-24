using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using CatDown.Core.Segmentation;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;

namespace CatDown.App.Preview;

// Read-only rendering of Markdown with Semi colors. Covers what translated documents need:
// headings, paragraphs with inline formatting, lists, quotes, code, tables, and rules.
public class MarkdownView : UserControl
{
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownView, string?>(nameof(Markdown));

    static readonly FontFamily Monospace = new("Cascadia Mono, Consolas, Menlo, monospace");
    static readonly double[] HeadingSizes = [28, 22, 18, 16, 15, 14];

    readonly StackPanel _root = new() { Spacing = 12, Margin = new Thickness(24, 16) };

    public MarkdownView()
    {
        Content = new ScrollViewer { Content = _root };
    }

    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public Panel RootPanel => _root;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkdownProperty)
            Render();
    }

    void Render()
    {
        _root.Children.Clear();
        if (string.IsNullOrEmpty(Markdown))
            return;
        var document = Markdig.Markdown.Parse(Markdown, BlockParser.Pipeline);
        AddBlocks(_root, document);
    }

    void AddBlocks(Panel panel, ContainerBlock container)
    {
        foreach (var block in container)
        {
            if (RenderBlock(block) is { } control)
                panel.Children.Add(control);
        }
    }

    Control? RenderBlock(MdBlock block) => block switch
    {
        YamlFrontMatterBlock => null,
        HeadingBlock heading => Text(heading.Inline, HeadingSizes[Math.Clamp(heading.Level, 1, 6) - 1], FontWeight.SemiBold),
        ParagraphBlock paragraph => Text(paragraph.Inline),
        ListBlock list => List(list),
        QuoteBlock quote => Quote(quote),
        Table table => TableGrid(table),
        CodeBlock code and not YamlFrontMatterBlock => Code(code),
        HtmlBlock html => Code(html),
        ThematicBreakBlock => Themed(new Border { Height = 1, Margin = new Thickness(0, 4) }, Border.BackgroundProperty, "SemiColorBorder"),
        ContainerBlock inner => Stack(inner),
        _ => null,
    };

    Control Stack(ContainerBlock container)
    {
        var panel = new StackPanel { Spacing = 8 };
        AddBlocks(panel, container);
        return panel;
    }

    SelectableTextBlock Text(ContainerInline? inlines, double fontSize = 14, FontWeight? weight = null)
    {
        var text = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, FontSize = fontSize, LineHeight = fontSize * 1.6 };
        if (weight is { } w)
            text.FontWeight = w;
        if (inlines is not null)
            AddInlines(text.Inlines!, inlines);
        return text;
    }

    void AddInlines(InlineCollection target, ContainerInline container)
    {
        foreach (var inline in container)
        {
            if (RenderInline(inline) is { } rendered)
                target.Add(rendered);
        }
    }

    Avalonia.Controls.Documents.Inline? RenderInline(MdInline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                return new Run(literal.Content.ToString());
            case LineBreakInline lineBreak:
                return lineBreak.IsHard ? new LineBreak() : new Run(" ");
            case CodeInline code:
                var run = new Run(code.Content) { FontFamily = Monospace };
                return Themed(run, TextElement.BackgroundProperty, "SemiColorFill0");
            case EmphasisInline emphasis:
                var span = emphasis.DelimiterCount >= 2
                    ? (Span)new Bold()
                    : emphasis.DelimiterChar == '~' ? new Span { TextDecorations = TextDecorations.Strikethrough } : new Italic();
                AddInlines(span.Inlines, emphasis);
                return span;
            case LinkInline { IsImage: true } image:
                var alt = new Italic();
                alt.Inlines.Add(new Run($"[{PlainText(image)}]"));
                return Themed(alt, TextElement.ForegroundProperty, "SemiColorText2");
            case LinkInline link:
                var underline = new Underline();
                AddInlines(underline.Inlines, link);
                return Themed(underline, TextElement.ForegroundProperty, "SemiColorLink");
            case AutolinkInline autolink:
                return Themed(new Underline { Inlines = { new Run(autolink.Url) } }, TextElement.ForegroundProperty, "SemiColorLink");
            case HtmlInline html:
                return Themed(new Run(html.Tag) { FontFamily = Monospace }, TextElement.ForegroundProperty, "SemiColorText2");
            case HtmlEntityInline entity:
                return new Run(entity.Transcoded.ToString());
            case TaskList:
                return null;
            case ContainerInline container:
                var group = new Span();
                AddInlines(group.Inlines, container);
                return group;
            default:
                return null;
        }
    }

    static string PlainText(ContainerInline container) =>
        string.Concat(container.Descendants<LiteralInline>().Select(l => l.Content.ToString()));

    Control List(ListBlock list)
    {
        var panel = new StackPanel { Spacing = 4 };
        var number = int.TryParse(list.OrderedStart, out var start) ? start : 1;
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var task = (item.FirstOrDefault() as ParagraphBlock)?.Inline?.FirstChild as TaskList;
            var marker = task is not null ? (task.Checked ? "☑" : "☐") : list.IsOrdered ? $"{number}." : "•";
            number++;
            var row = new DockPanel();
            var bullet = new TextBlock { Text = marker, MinWidth = 22, Margin = new Thickness(0, 0, 6, 0), TextAlignment = TextAlignment.Right };
            DockPanel.SetDock(bullet, Dock.Left);
            row.Children.Add(bullet);
            row.Children.Add(Stack(item));
            panel.Children.Add(row);
        }
        return panel;
    }

    Control Quote(QuoteBlock quote)
    {
        var content = Stack(quote);
        Themed(content, TextBlock.ForegroundProperty, "SemiColorText2");
        var border = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 2, 0, 2),
            Child = content,
        };
        return Themed(border, Border.BorderBrushProperty, "SemiColorBorder");
    }

    Control Code(LeafBlock block)
    {
        var lines = new List<string>();
        for (var i = 0; i < block.Lines.Count; i++)
            lines.Add(block.Lines.Lines[i].ToString());
        var text = new SelectableTextBlock { Text = string.Join("\n", lines), FontFamily = Monospace, FontSize = 13 };
        var border = new Border { Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(4), Child = text };
        return Themed(border, Border.BackgroundProperty, "SemiColorFill0");
    }

    Control TableGrid(Table table)
    {
        var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
        var rows = table.OfType<TableRow>().ToList();
        var columns = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        for (var c = 0; c < columns; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < rows[r].Count; c++)
            {
                var cell = (TableCell)rows[r][c];
                var content = Stack(cell);
                if (rows[r].IsHeader)
                    content.SetValue(TextBlock.FontWeightProperty, FontWeight.SemiBold);
                var border = new Border
                {
                    BorderThickness = new Thickness(c == 0 ? 1 : 0, r == 0 ? 1 : 0, 1, 1),
                    Padding = new Thickness(10, 6),
                    MaxWidth = 420,
                    Child = content,
                };
                Themed(border, Border.BorderBrushProperty, "SemiColorBorder");
                if (rows[r].IsHeader)
                    Themed(border, Border.BackgroundProperty, "SemiColorFill0");
                Grid.SetRow(border, r);
                Grid.SetColumn(border, c);
                grid.Children.Add(border);
            }
        }
        return grid;
    }

    T Themed<T>(T target, AvaloniaProperty property, string resource) where T : AvaloniaObject
    {
        target.Bind(property, this.GetResourceObservable(resource));
        return target;
    }
}
