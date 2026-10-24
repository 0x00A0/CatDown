namespace CatDown.Core.Segmentation;

public interface ISentenceSplitter
{
    // Returns sentence ranges in text, in order. Whitespace between sentences belongs to none of them.
    IReadOnlyList<TextSpan> Split(string text, IReadOnlyList<TextSpan> protectedSpans, string language);
}
