using CatDown.Core.Llm;
using CatDown.Core.Model;
using CatDown.Core.Segmentation;

namespace CatDown.Core.Editing;

// What to do with a block that already has translations when its sentences are split again.
public enum TranslatedBlockAction
{
    Skip,
    MergeIntoFirst,
    LlmRealign,
}

public sealed record ResegmentStats(int ChangedBlocks, int SkippedBlocks);

public sealed class Resegmenter(string targetLanguage, Func<Block, Task<TranslatedBlockAction>> askForTranslatedBlock, LlmRealigner? realigner = null)
{
    // Splits the given blocks again. `split` returns the new sentence ranges of a block's text.
    public async Task<ResegmentStats> ResegmentAsync(FileSession session, IEnumerable<Block> blocks,
        Func<Block, ParsedBlock, Task<IReadOnlyList<TextSpan>>> split)
    {
        int changed = 0, skipped = 0;
        foreach (var block in blocks.Where(b => !b.Verbatim).ToList())
        {
            var parsed = session.ParsedBlockOf(block);
            var spans = await split(block, parsed);
            var sources = spans.Select(s => block.Source.Substring(s.Start, s.Length)).ToList();
            if (sources.Count == 0 || sources.SequenceEqual(block.Segments.Select(s => s.Source)))
                continue;

            if (await ApplyAsync(session.Document, block, sources))
                changed++;
            else
                skipped++;
        }
        if (changed > 0)
            session.Save();
        return new ResegmentStats(changed, skipped);
    }

    async Task<bool> ApplyAsync(FileDocument document, Block block, List<string> sources)
    {
        var translated = block.Segments.Where(s => s.HasTranslation && s.Target.Length > 0).ToList();
        var segments = sources.Select((source, i) => new Segment
        {
            Id = i == 0 ? block.Segments[0].Id : document.NewSegmentId(),
            Source = source,
        }).ToList();

        if (translated.Count > 0)
        {
            var action = await askForTranslatedBlock(block);
            if (action == TranslatedBlockAction.Skip)
                return false;

            var translation = string.Join(Languages.SentenceSeparator(targetLanguage), translated.Select(s => s.Target));
            IReadOnlyList<string>? parts = null;
            if (action == TranslatedBlockAction.LlmRealign && realigner is not null)
                parts = await realigner.RealignAsync(translation, sources);
            parts ??= sources.Select((_, i) => i == 0 ? translation : "").ToList();

            for (var i = 0; i < segments.Count; i++)
            {
                segments[i].Target = parts[i];
                segments[i].Status = SegmentStatus.NeedsReview;
            }
        }

        block.Segments = segments;
        return true;
    }
}
