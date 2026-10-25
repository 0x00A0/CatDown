using System.Text;
using CatDown.Core.Model;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CatDown.Core.Segmentation;

public static class BlockParser
{
    public static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation()
        .UseYamlFrontMatter()
        .UsePipeTables()
        .UseTaskLists()
        .Build();

    public static ParsedFile Parse(string text)
    {
        var document = Markdown.Parse(text, Pipeline);
        var blocks = new List<ParsedBlock>();
        Walk(document, text, blocks);
        return new ParsedFile
        {
            Text = text,
            NewLine = text.Contains("\r\n") ? "\r\n" : "\n",
            Blocks = blocks,
        };
    }

    static void Walk(ContainerBlock container, string text, List<ParsedBlock> blocks)
    {
        foreach (var child in container)
        {
            switch (child)
            {
                case YamlFrontMatterBlock front:
                    blocks.Add(Verbatim(BlockKind.FrontMatter, front, text));
                    break;
                case CodeBlock code:
                    blocks.Add(Verbatim(BlockKind.CodeBlock, code, text));
                    break;
                case HtmlBlock html:
                    blocks.Add(Verbatim(BlockKind.Html, html, text));
                    break;
                case HeadingBlock heading:
                    AddText(BlockKind.Heading, heading, text, blocks);
                    break;
                case ParagraphBlock paragraph:
                    var kind = paragraph.Parent is TableCell ? BlockKind.TableCell : BlockKind.Paragraph;
                    AddText(kind, paragraph, text, blocks);
                    break;
                case LinkReferenceDefinitionGroup:
                    break;
                case ContainerBlock inner:
                    Walk(inner, text, blocks);
                    break;
            }
        }
    }

    static ParsedBlock Verbatim(BlockKind kind, Markdig.Syntax.Block block, string text)
    {
        // A fence closed at the very end of the file can report a span one past the text.
        var end = Math.Min(block.Span.End + 1, text.Length);
        while (end > block.Span.Start && text[end - 1] is '\n' or '\r')
            end--;
        var span = TextSpan.FromBounds(block.Span.Start, end);
        return new ParsedBlock
        {
            Kind = kind,
            Verbatim = true,
            SourceSpan = span,
            Text = text.Substring(span.Start, span.Length),
        };
    }

    static void AddText(BlockKind kind, LeafBlock leaf, string text, List<ParsedBlock> blocks)
    {
        if (ContentRange(leaf) is not { } content)
            return;

        // "- [ ] task": the checkbox belongs to the skeleton.
        var start = content.Start;
        if (leaf.Inline?.FirstChild is TaskList task)
            start = Math.Max(start, task.Span.End + 1);

        var ranges = LineRanges(text, start, content.End);
        if (ranges.Count == 0)
            return;

        var pieces = new List<LinePiece>();
        var prefixes = new List<string>();
        var logical = new StringBuilder();
        for (var i = 0; i < ranges.Count; i++)
        {
            if (i > 0)
            {
                logical.Append('\n');
                prefixes.Add(PrefixBetween(text, ranges[i - 1].End, ranges[i].Start));
            }
            pieces.Add(new LinePiece(ranges[i].Start, logical.Length, ranges[i].Length));
            logical.Append(text, ranges[i].Start, ranges[i].Length);
        }

        blocks.Add(new ParsedBlock
        {
            Kind = kind,
            SourceSpan = TextSpan.FromBounds(ranges[0].Start, ranges[^1].End),
            Text = logical.ToString(),
            Pieces = pieces,
            ContinuationPrefixes = prefixes,
            DefaultContinuationPrefix = DefaultPrefix(text, ranges[0].Start),
            Protected = ProtectedSpans(leaf, pieces),
        });
    }

    // Markdig drops a leaf's Lines once its inlines are parsed, so the inlines give the range.
    static TextSpan? ContentRange(LeafBlock leaf)
    {
        if (leaf.Inline?.FirstChild is { } first)
            return TextSpan.FromBounds(first.Span.Start, leaf.Inline.LastChild!.Span.End + 1);
        if (leaf.Lines.Count > 0)
            return TextSpan.FromBounds(leaf.Lines.Lines[0].Slice.Start, leaf.Lines.Lines[leaf.Lines.Count - 1].Slice.End + 1);
        return null;
    }

    // Splits a block's source range into lines without their container prefixes.
    // A paragraph's continuation line never starts with '>', otherwise it would be a quote,
    // so leading whitespace and '>' are always prefix.
    static List<TextSpan> LineRanges(string text, int start, int end)
    {
        var ranges = new List<TextSpan>();
        var lineStart = start;
        while (lineStart < end)
        {
            var newline = text.IndexOf('\n', lineStart, end - lineStart);
            var lineEnd = newline < 0 ? end : newline;
            var range = Trim(text, lineStart, lineEnd);
            if (range.Length > 0)
                ranges.Add(range);
            if (newline < 0)
                break;
            lineStart = newline + 1;
            while (lineStart < end && (text[lineStart] == '>' || char.IsWhiteSpace(text[lineStart])))
                lineStart++;
        }
        return ranges;
    }

    static TextSpan Trim(string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start])) start++;
        while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
        return TextSpan.FromBounds(start, end);
    }

    static string PrefixBetween(string text, int previousEnd, int nextStart)
    {
        var newline = text.IndexOf('\n', previousEnd, nextStart - previousEnd);
        return newline < 0 ? "" : text.Substring(newline + 1, nextStart - newline - 1);
    }

    static string DefaultPrefix(string text, int start)
    {
        var lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        var lead = text.AsSpan(lineStart, start - lineStart);
        var prefix = new char[lead.Length];
        for (var i = 0; i < lead.Length; i++)
            prefix[i] = lead[i] == '>' || char.IsWhiteSpace(lead[i]) ? lead[i] : ' ';
        return new string(prefix);
    }

    static List<TextSpan> ProtectedSpans(LeafBlock leaf, List<LinePiece> pieces)
    {
        var spans = new List<TextSpan>();
        if (leaf.Inline is null)
            return spans;

        foreach (var inline in leaf.Inline.Descendants())
        {
            if (inline is not (CodeInline or LinkInline or AutolinkInline or EmphasisInline or HtmlInline))
                continue;
            var start = ToLogical(pieces, inline.Span.Start);
            var end = ToLogical(pieces, inline.Span.End + 1);
            if (start is { } s && end is { } e && e > s)
                spans.Add(TextSpan.FromBounds(s, e));
        }
        return spans;
    }

    static int? ToLogical(List<LinePiece> pieces, int sourceOffset)
    {
        foreach (var piece in pieces)
        {
            if (sourceOffset >= piece.SourceStart && sourceOffset <= piece.SourceStart + piece.Length)
                return piece.TextStart + sourceOffset - piece.SourceStart;
        }
        return null;
    }
}
