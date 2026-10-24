using CatDown.Core.Export;
using CatDown.Core.Segmentation;

namespace CatDown.Core.Editing;

public static class SourceEditor
{
    // Replaces one block of the source file with new logical text and returns the new file text.
    public static string ReplaceBlock(ParsedFile file, int blockIndex, string newText)
    {
        var parsed = file.Blocks[blockIndex];
        if (newText == parsed.Text)
            return file.Text;

        var replacement = Exporter.ToSource(newText, parsed, file.NewLine);
        return string.Concat(
            file.Text.AsSpan(0, parsed.SourceSpan.Start),
            replacement,
            file.Text.AsSpan(parsed.SourceSpan.End));
    }
}
