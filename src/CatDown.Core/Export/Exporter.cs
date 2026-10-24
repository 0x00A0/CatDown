using System.Text;
using CatDown.Core.Model;
using CatDown.Core.Segmentation;

namespace CatDown.Core.Export;

public sealed record ExportResult(string Text, int UntranslatedCount);

public static class Exporter
{
    // The document's blocks must line up one to one with the parsed file's blocks.
    public static ExportResult Export(ParsedFile file, FileDocument document, string targetLanguage)
    {
        if (file.Blocks.Count != document.Blocks.Count)
            throw new InvalidOperationException("Document blocks do not match the source file.");

        var separator = Languages.SentenceSeparator(targetLanguage);
        var output = new StringBuilder(file.Text.Length);
        var untranslated = 0;
        var cursor = 0;
        for (var i = 0; i < file.Blocks.Count; i++)
        {
            var parsed = file.Blocks[i];
            var block = document.Blocks[i];
            if (block.Source != parsed.Text)
                throw new InvalidOperationException($"Block {block.Id} does not match the source file.");

            output.Append(file.Text, cursor, parsed.SourceSpan.Start - cursor);
            var logical = BlockText(block, separator, ref untranslated);
            output.Append(logical == parsed.Text
                ? file.Text.AsSpan(parsed.SourceSpan.Start, parsed.SourceSpan.Length)
                : ToSource(logical, parsed, file.NewLine));
            cursor = parsed.SourceSpan.End;
        }
        output.Append(file.Text, cursor, file.Text.Length - cursor);
        return new ExportResult(output.ToString(), untranslated);
    }

    // The block's translated logical text: segment translations joined by the original
    // whitespace, or by the target language's separator when both neighbours are translated.
    public static string BlockText(Block block, string separator, ref int untranslated)
    {
        var text = new StringBuilder();
        var cursor = 0;
        Segment? previous = null;
        foreach (var segment in block.Segments)
        {
            var start = block.Source.IndexOf(segment.Source, cursor, StringComparison.Ordinal);
            if (start < 0)
                throw new InvalidOperationException($"Segment {segment.Id} is not part of block {block.Id}.");

            if (previous is not null)
                text.Append(IsTranslated(previous) && IsTranslated(segment) ? separator : block.Source[cursor..start]);

            if (IsTranslated(segment))
            {
                text.Append(segment.Target);
            }
            else
            {
                text.Append(segment.Source);
                untranslated++;
            }
            cursor = start + segment.Source.Length;
            previous = segment;
        }
        return text.ToString();
    }

    static bool IsTranslated(Segment segment) => segment.HasTranslation && segment.Target.Length > 0;

    static string ToSource(string logical, ParsedBlock parsed, string newLine)
    {
        logical = logical.Replace("\r\n", "\n");
        if (parsed.Verbatim)
            return logical.Replace("\n", newLine);

        switch (parsed.Kind)
        {
            case BlockKind.Heading:
                return logical.Replace('\n', ' ');
            case BlockKind.TableCell:
                return EscapePipes(logical.Replace('\n', ' '));
        }

        var lines = logical.Split('\n');
        var text = new StringBuilder(lines[0]);
        for (var k = 1; k < lines.Length; k++)
        {
            var prefix = k - 1 < parsed.ContinuationPrefixes.Count ? parsed.ContinuationPrefixes[k - 1]
                : parsed.ContinuationPrefixes.Count > 0 ? parsed.ContinuationPrefixes[^1]
                : parsed.DefaultContinuationPrefix;
            text.Append(newLine).Append(prefix).Append(lines[k]);
        }
        return text.ToString();
    }

    static string EscapePipes(string text)
    {
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '|' && (i == 0 || text[i - 1] != '\\'))
                result.Append('\\');
            result.Append(text[i]);
        }
        return result.ToString();
    }
}
