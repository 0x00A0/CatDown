namespace CatDown.App.Editor;

public enum InlineFormat
{
    Bold,
    Italic,
    Code,
    Link,
}

public readonly record struct FormatResult(string Text, int SelectionStart, int SelectionLength);

public static class MarkdownFormatting
{
    // Wraps the selection in Markdown markup. With an empty selection the caret ends up
    // between the markers; for links the "url" placeholder is selected.
    public static FormatResult Apply(string text, int start, int length, InlineFormat format)
    {
        var selected = text.Substring(start, length);
        var (before, after) = format switch
        {
            InlineFormat.Bold => ("**", "**"),
            InlineFormat.Italic => ("*", "*"),
            InlineFormat.Code => ("`", "`"),
            _ => ("[", "](url)"),
        };

        var newText = text[..start] + before + selected + after + text[(start + length)..];
        if (format == InlineFormat.Link)
            return new FormatResult(newText, start + before.Length + selected.Length + 2, 3);
        return new FormatResult(newText, start + before.Length, selected.Length);
    }
}
