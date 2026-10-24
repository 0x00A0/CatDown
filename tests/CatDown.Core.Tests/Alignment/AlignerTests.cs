using CatDown.Core.Alignment;
using CatDown.Core.Export;
using CatDown.Core.Model;
using CatDown.Core.Segmentation;

namespace CatDown.Core.Tests.Alignment;

public class AlignerTests
{
    static readonly DocumentBuilder Builder = new(new RuleSentenceSplitter(), "en");

    // Builds a document for the markdown and translates every text segment as "<source>-zh".
    static FileDocument Translated(string markdown)
    {
        var doc = Builder.Build("a.md", BlockParser.Parse(markdown));
        foreach (var segment in doc.Blocks.Where(b => !b.Verbatim).SelectMany(b => b.Segments))
        {
            segment.Target = segment.Source + "-zh";
            segment.Status = SegmentStatus.Confirmed;
        }
        return doc;
    }

    static AlignmentResult Align(FileDocument old, string markdown) => new Aligner(Builder).Align(old, BlockParser.Parse(markdown));

    static List<(string Source, string Target, SegmentStatus Status)> Rows(FileDocument doc) =>
        doc.AllSegments.Select(s => (s.Source, s.Target, s.Status)).ToList();

    [Fact]
    public void UnchangedSourceKeepsEverything()
    {
        var old = Translated("One. Two.\n\nThree.\n");
        var before = Rows(old);

        var result = Align(old, "One. Two.\n\nThree.\n");

        Assert.Equal(before, Rows(result.Document));
        Assert.False(result.Stats.HasChanges);
        Assert.Equal(3, result.Stats.Kept);
    }

    [Fact]
    public void InsertedParagraphIsNew()
    {
        var result = Align(Translated("One.\n\nThree.\n"), "One.\n\nTwo.\n\nThree.\n");

        Assert.Equal(
            [("One.", "One.-zh", SegmentStatus.Confirmed), ("Two.", "", SegmentStatus.Untranslated), ("Three.", "Three.-zh", SegmentStatus.Confirmed)],
            Rows(result.Document));
        Assert.Equal(new AlignmentStats(2, 0, 1, 0), result.Stats);
    }

    [Fact]
    public void DeletedParagraphIsDropped()
    {
        var result = Align(Translated("One.\n\nTwo.\n\nThree.\n"), "One.\n\nThree.\n");

        Assert.Equal([("One.", "One.-zh", SegmentStatus.Confirmed), ("Three.", "Three.-zh", SegmentStatus.Confirmed)], Rows(result.Document));
        Assert.Equal(new AlignmentStats(2, 0, 0, 1), result.Stats);
    }

    [Fact]
    public void ChangedSentenceNeedsReviewAndNeighboursStay()
    {
        var old = Translated("One. Two. Three.\n");
        var ids = old.AllSegments.Select(s => s.Id).ToList();

        var result = Align(old, "One. Two changed. Three.\n");

        Assert.Equal(
            [("One.", "One.-zh", SegmentStatus.Confirmed), ("Two changed.", "Two.-zh", SegmentStatus.NeedsReview), ("Three.", "Three.-zh", SegmentStatus.Confirmed)],
            Rows(result.Document));
        Assert.Equal(ids, result.Document.AllSegments.Select(s => s.Id));
        Assert.Equal(new AlignmentStats(2, 1, 0, 0), result.Stats);
    }

    [Fact]
    public void SentenceAddedAndRemovedInsideParagraph()
    {
        var result = Align(Translated("One. Two. Three.\n"), "One. Three. Four.\n");

        Assert.Equal(
            [("One.", "One.-zh", SegmentStatus.Confirmed), ("Three.", "Three.-zh", SegmentStatus.Confirmed), ("Four.", "", SegmentStatus.Untranslated)],
            Rows(result.Document));
        Assert.Equal(new AlignmentStats(2, 0, 1, 1), result.Stats);
    }

    [Fact]
    public void SwappedParagraphsBothMatch()
    {
        var result = Align(Translated("Alpha.\n\nBeta.\n"), "Beta.\n\nAlpha.\n");

        Assert.Equal([("Beta.", "Beta.-zh", SegmentStatus.Confirmed), ("Alpha.", "Alpha.-zh", SegmentStatus.Confirmed)], Rows(result.Document));
        Assert.False(result.Stats.HasChanges);
    }

    [Fact]
    public void UntouchedCodeBlockFollowsSource()
    {
        var result = Align(Translated("```\nold\n```\n"), "```\nnew\n```\n");

        Assert.Equal([("```\nnew\n```", "```\nnew\n```", SegmentStatus.Confirmed)], Rows(result.Document));
    }

    [Fact]
    public void EditedCodeBlockNeedsReview()
    {
        var old = Translated("```\n# comment\nold\n```\n");
        old.AllSegments.Single().Target = "```\n# 注释\nold\n```";

        var result = Align(old, "```\n# comment\nnew\n```\n");

        Assert.Equal([("```\n# comment\nnew\n```", "```\n# 注释\nold\n```", SegmentStatus.NeedsReview)], Rows(result.Document));
        Assert.Equal(1, result.Stats.NeedsReview);
    }

    [Fact]
    public void AlignedDocumentExports()
    {
        var markdown = "# Title\n\nOne. Two changed.\n\n- New item.\n";
        var result = Align(Translated("# Title\n\nOne. Two.\n"), markdown);

        var exported = Exporter.Export(BlockParser.Parse(markdown), result.Document, "zh").Text;

        Assert.Equal("# Title-zh\n\nOne.-zhTwo.-zh\n\n- New item.\n", exported);
    }

    [Fact]
    public void NewIdsContinueFromOldDocument()
    {
        var old = Translated("One.\n");
        var result = Align(old, "One.\n\nTwo.\n");
        var ids = result.Document.Blocks.Select(b => b.Id).Concat(result.Document.AllSegments.Select(s => s.Id)).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
