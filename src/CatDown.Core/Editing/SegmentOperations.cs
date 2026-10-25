using CatDown.Core.Model;
using CatDown.Core.Segmentation;

namespace CatDown.Core.Editing;

public enum SegmentEditResult
{
    Done,
    NotFound,
    NoNextSegment,
    Verbatim,
    EmptySide,
    InsideMarkup,
}

public static class SegmentOperations
{
    // Merges the segment with the next one in the same block.
    public static SegmentEditResult Merge(FileDocument document, string segmentId, string targetLanguage)
    {
        if (Find(document, segmentId) is not var (block, index))
            return SegmentEditResult.NotFound;
        if (index + 1 >= block.Segments.Count)
            return SegmentEditResult.NoNextSegment;

        var first = block.Segments[index];
        var second = block.Segments[index + 1];
        var firstStart = StartOf(block, index);
        var secondStart = StartOf(block, index + 1);

        first.Source = block.Source[firstStart..(secondStart + second.Source.Length)];
        first.Target = string.Join(Languages.SentenceSeparator(targetLanguage),
            new[] { first.Target, second.Target }.Where(t => t.Length > 0));
        first.Status = first.HasTranslation || second.HasTranslation ? SegmentStatus.Draft : SegmentStatus.Untranslated;
        block.Segments.RemoveAt(index + 1);
        return SegmentEditResult.Done;
    }

    // Splits the segment at an offset in its source text. The translation stays with the first half.
    public static SegmentEditResult Split(FileDocument document, ParsedBlock parsed, string segmentId, int offset)
    {
        if (Find(document, segmentId) is not var (block, index))
            return SegmentEditResult.NotFound;
        if (block.Verbatim)
            return SegmentEditResult.Verbatim;

        var segment = block.Segments[index];
        var left = segment.Source[..Math.Clamp(offset, 0, segment.Source.Length)].TrimEnd();
        var right = segment.Source[Math.Clamp(offset, 0, segment.Source.Length)..].TrimStart();
        if (left.Length == 0 || right.Length == 0)
            return SegmentEditResult.EmptySide;

        var blockOffset = StartOf(block, index) + offset;
        if (parsed.Protected.Any(p => p.Start < blockOffset && blockOffset < p.End))
            return SegmentEditResult.InsideMarkup;

        segment.Source = left;
        if (segment.Status == SegmentStatus.Confirmed)
            segment.Status = SegmentStatus.NeedsReview;
        block.Segments.Insert(index + 1, new Segment { Id = document.NewSegmentId(), Source = right });
        return SegmentEditResult.Done;
    }

    public static (Block Block, int Index)? Find(FileDocument document, string segmentId)
    {
        foreach (var block in document.Blocks)
        {
            var index = block.Segments.FindIndex(s => s.Id == segmentId);
            if (index >= 0)
                return (block, index);
        }
        return null;
    }

    // Where segment number `index` starts in its block's source text.
    public static int StartOf(Block block, int index)
    {
        var cursor = 0;
        for (var i = 0; ; i++)
        {
            var start = block.Source.IndexOf(block.Segments[i].Source, cursor, StringComparison.Ordinal);
            if (start < 0)
                throw new InvalidOperationException($"Segment {block.Segments[i].Id} is not part of block {block.Id}.");
            if (i == index)
                return start;
            cursor = start + block.Segments[i].Source.Length;
        }
    }
}
