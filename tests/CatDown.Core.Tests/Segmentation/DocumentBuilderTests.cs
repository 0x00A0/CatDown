using CatDown.Core.Model;
using CatDown.Core.Segmentation;

namespace CatDown.Core.Tests.Segmentation;

public class DocumentBuilderTests
{
    static FileDocument Build(string markdown) =>
        new DocumentBuilder(new RuleSentenceSplitter(), "en").Build("docs/setup.md", BlockParser.Parse(markdown));

    [Fact]
    public void BuildsBlocksAndSegments()
    {
        var doc = Build(Samples.AllKinds);

        Assert.Equal(
            [
                (BlockKind.FrontMatter, 1),
                (BlockKind.Heading, 1),
                (BlockKind.Paragraph, 2),
                (BlockKind.CodeBlock, 1),
                (BlockKind.Paragraph, 2),
                (BlockKind.Paragraph, 1),
                (BlockKind.Paragraph, 1),
                (BlockKind.Paragraph, 1),
                (BlockKind.TableCell, 1),
                (BlockKind.TableCell, 1),
                (BlockKind.TableCell, 1),
                (BlockKind.TableCell, 2),
                (BlockKind.Html, 1),
                (BlockKind.Paragraph, 1),
            ],
            doc.Blocks.Select(b => (b.Kind, b.Segments.Count)).ToList());
        Assert.Equal(["Install the **CLI** with the command below.", "Then run it."], doc.Blocks[2].Segments.Select(s => s.Source));
        Assert.Equal("docs/setup.md", doc.SourcePath);
        Assert.StartsWith("sha256:", doc.SourceHash);
    }

    [Fact]
    public void VerbatimBlocksArePrefilledAndConfirmed()
    {
        var doc = Build(Samples.AllKinds);
        foreach (var block in doc.Blocks.Where(b => b.Verbatim))
        {
            var segment = Assert.Single(block.Segments);
            Assert.Equal(block.Source, segment.Target);
            Assert.Equal(SegmentStatus.Confirmed, segment.Status);
        }
        Assert.Equal("```bash\nnpm i -g foo\n```", doc.Blocks[3].Source);
    }

    [Fact]
    public void TextSegmentsStartUntranslated()
    {
        var doc = Build(Samples.AllKinds);
        Assert.All(doc.Blocks.Where(b => !b.Verbatim).SelectMany(b => b.Segments), s =>
        {
            Assert.Equal(SegmentStatus.Untranslated, s.Status);
            Assert.Equal("", s.Target);
        });
    }

    [Fact]
    public void IdsAreUnique()
    {
        var doc = Build(Samples.AllKinds);
        var ids = doc.Blocks.Select(b => b.Id).Concat(doc.AllSegments.Select(s => s.Id)).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void HashChangesWithText()
    {
        Assert.Equal(DocumentBuilder.Hash("a"), DocumentBuilder.Hash("a"));
        Assert.NotEqual(DocumentBuilder.Hash("a"), DocumentBuilder.Hash("b"));
    }
}
