using CatDown.Core.Editing;
using CatDown.Core.Export;
using CatDown.Core.Model;
using CatDown.Core.Segmentation;

namespace CatDown.Core.Tests.Editing;

public class SegmentOperationsTests
{
    static (ParsedFile File, FileDocument Doc) Open(string markdown)
    {
        var file = BlockParser.Parse(markdown);
        return (file, new DocumentBuilder(new RuleSentenceSplitter(), "en").Build("a.md", file));
    }

    [Fact]
    public void MergeJoinsSourceWithOriginalWhitespaceAndTargets()
    {
        var (file, doc) = Open("One.  Two. Three.\n");
        var segments = doc.AllSegments.ToList();
        segments[0].Target = "一。";
        segments[0].Status = SegmentStatus.Confirmed;
        segments[1].Target = "二。";
        segments[1].Status = SegmentStatus.Confirmed;

        Assert.Equal(SegmentEditResult.Done, SegmentOperations.Merge(doc, segments[0].Id, "zh"));

        var merged = doc.AllSegments.First();
        Assert.Equal("One.  Two.", merged.Source);
        Assert.Equal("一。二。", merged.Target);
        Assert.Equal(SegmentStatus.Draft, merged.Status);
        Assert.Equal(2, doc.AllSegments.Count());
        Assert.Equal("一。二。 Three.\n", Exporter.Export(file, doc, "zh").Text);
    }

    [Fact]
    public void MergingUntranslatedStaysUntranslated()
    {
        var (_, doc) = Open("One. Two.\n");
        SegmentOperations.Merge(doc, doc.AllSegments.First().Id, "en");
        Assert.Equal(SegmentStatus.Untranslated, doc.AllSegments.Single().Status);
    }

    [Fact]
    public void CannotMergeLastSegmentOfBlock()
    {
        var (_, doc) = Open("One. Two.\n\nThree.\n");
        Assert.Equal(SegmentEditResult.NoNextSegment, SegmentOperations.Merge(doc, doc.AllSegments.ElementAt(1).Id, "zh"));
        Assert.Equal(3, doc.AllSegments.Count());
    }

    [Fact]
    public void SplitKeepsTranslationInFirstHalf()
    {
        var (file, doc) = Open("First part, second part.\n");
        var segment = doc.AllSegments.Single();
        segment.Target = "整句。";
        segment.Status = SegmentStatus.Confirmed;

        var result = SegmentOperations.Split(doc, file.Blocks[0], segment.Id, "First part,".Length);

        Assert.Equal(SegmentEditResult.Done, result);
        Assert.Equal(
            [("First part,", "整句。", SegmentStatus.NeedsReview), ("second part.", "", SegmentStatus.Untranslated)],
            doc.AllSegments.Select(s => (s.Source, s.Target, s.Status)).ToList());
        Assert.Equal("整句。 second part.\n", Exporter.Export(file, doc, "zh").Text);
    }

    [Fact]
    public void CannotSplitInsideInlineCode()
    {
        var (file, doc) = Open("Run `a b c` now.\n");
        var segment = doc.AllSegments.Single();
        Assert.Equal(SegmentEditResult.InsideMarkup, SegmentOperations.Split(doc, file.Blocks[0], segment.Id, "Run `a".Length));
    }

    [Fact]
    public void CannotSplitAtEdges()
    {
        var (file, doc) = Open("Text here.\n");
        var segment = doc.AllSegments.Single();
        Assert.Equal(SegmentEditResult.EmptySide, SegmentOperations.Split(doc, file.Blocks[0], segment.Id, 0));
        Assert.Equal(SegmentEditResult.EmptySide, SegmentOperations.Split(doc, file.Blocks[0], segment.Id, segment.Source.Length));
    }

    [Fact]
    public void CannotSplitVerbatimBlock()
    {
        var (file, doc) = Open("```\na b\n```\n");
        Assert.Equal(SegmentEditResult.Verbatim, SegmentOperations.Split(doc, file.Blocks[0], doc.AllSegments.Single().Id, 4));
    }

    [Fact]
    public void SplitThenMergeRestoresSource()
    {
        var (file, doc) = Open("Alpha beta gamma.\n");
        var id = doc.AllSegments.Single().Id;
        SegmentOperations.Split(doc, file.Blocks[0], id, "Alpha beta".Length);
        SegmentOperations.Merge(doc, id, "en");
        Assert.Equal("Alpha beta gamma.", doc.AllSegments.Single().Source);
    }
}
