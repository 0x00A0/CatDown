using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace CatDown.App.Editor;

// Locked ranges that cannot be typed into or deleted. Unlike AvaloniaEdit's own provider,
// a deletion that touches a locked range is refused as a whole instead of deleting around it.
public sealed class AtomicReadOnlySectionProvider(TextSegmentCollection<TextSegment> locked) : IReadOnlySectionProvider
{
    public bool CanInsert(int offset) =>
        !locked.FindSegmentsContaining(offset).Any(s => s.StartOffset < offset && offset < s.EndOffset);

    // FindOverlappingSegments also returns ranges that merely touch; only a real overlap blocks.
    public IEnumerable<ISegment> GetDeletableSegments(ISegment segment) =>
        locked.FindOverlappingSegments(segment.Offset, segment.Length)
            .Any(s => s.StartOffset < segment.EndOffset && segment.Offset < s.EndOffset) ? [] : [segment];
}
