using CatDown.Core.Model;
using CatDown.Core.Segmentation;

namespace CatDown.Core.Alignment;

public sealed record AlignmentStats(int Kept, int NeedsReview, int Added, int Removed)
{
    public bool HasChanges => NeedsReview > 0 || Added > 0 || Removed > 0;
}

public sealed record AlignmentResult(FileDocument Document, AlignmentStats Stats);

// Carries translations over to a new version of the source file.
public sealed class Aligner(DocumentBuilder builder)
{
    public AlignmentResult Align(FileDocument old, ParsedFile file)
    {
        var merged = new FileDocument
        {
            SourcePath = old.SourcePath,
            SourceHash = DocumentBuilder.Hash(file.Text),
            NextId = old.NextId,
        };
        int kept = 0, review = 0, added = 0, removed = 0;

        var pairs = SequenceAligner.Align(old.Blocks.Select(b => b.Source).ToList(), file.Blocks.Select(b => b.Text).ToList());
        foreach (var pair in pairs)
        {
            switch (pair.Kind)
            {
                case PairKind.Same:
                    var same = old.Blocks[pair.Old];
                    same.Kind = file.Blocks[pair.New].Kind;
                    merged.Blocks.Add(same);
                    kept += same.Segments.Count;
                    break;

                case PairKind.Added:
                    var fresh = builder.BuildBlock(merged, file.Blocks[pair.New]);
                    merged.Blocks.Add(fresh);
                    added += fresh.Segments.Count;
                    break;

                case PairKind.Removed:
                    removed += old.Blocks[pair.Old].Segments.Count;
                    break;

                case PairKind.Changed:
                    var block = builder.BuildBlock(merged, file.Blocks[pair.New]);
                    block.Id = old.Blocks[pair.Old].Id;
                    var stats = CarryOver(old.Blocks[pair.Old], block);
                    merged.Blocks.Add(block);
                    kept += stats.Kept;
                    review += stats.NeedsReview;
                    added += stats.Added;
                    removed += stats.Removed;
                    break;
            }
        }

        return new AlignmentResult(merged, new AlignmentStats(kept, review, added, removed));
    }

    static AlignmentStats CarryOver(Block old, Block block)
    {
        if (block.Verbatim)
            return CarryOverVerbatim(old.Segments[0], block.Segments[0]);

        int kept = 0, review = 0, added = 0, removed = 0;
        var fresh = block.Segments.ToList();
        block.Segments.Clear();
        var pairs = SequenceAligner.Align(old.Segments.Select(s => s.Source).ToList(), fresh.Select(s => s.Source).ToList());
        foreach (var pair in pairs)
        {
            switch (pair.Kind)
            {
                case PairKind.Same:
                    block.Segments.Add(old.Segments[pair.Old]);
                    kept++;
                    break;
                case PairKind.Added:
                    block.Segments.Add(fresh[pair.New]);
                    added++;
                    break;
                case PairKind.Removed:
                    removed++;
                    break;
                case PairKind.Changed:
                    var previous = old.Segments[pair.Old];
                    var segment = fresh[pair.New];
                    segment.Id = previous.Id;
                    if (previous.HasTranslation)
                    {
                        segment.Target = previous.Target;
                        segment.Status = SegmentStatus.NeedsReview;
                        review++;
                    }
                    else
                    {
                        added++;
                    }
                    block.Segments.Add(segment);
                    break;
            }
        }
        return new AlignmentStats(kept, review, added, removed);
    }

    // An untouched code block simply follows the source; an edited one keeps the edit for review.
    static AlignmentStats CarryOverVerbatim(Segment old, Segment segment)
    {
        segment.Id = old.Id;
        if (old.Target == old.Source)
            return new AlignmentStats(1, 0, 0, 0);

        segment.Target = old.Target;
        segment.Status = SegmentStatus.NeedsReview;
        return new AlignmentStats(0, 1, 0, 0);
    }
}
