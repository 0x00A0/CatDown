using CatDown.Core.Export;
using CatDown.Core.Model;
using CatDown.Core.Segmentation;

namespace CatDown.Core.Tests.Export;

public class ExporterTests
{
    static (ParsedFile File, FileDocument Doc) Open(string markdown)
    {
        var file = BlockParser.Parse(markdown);
        return (file, new DocumentBuilder(new RuleSentenceSplitter(), "en").Build("a.md", file));
    }

    static void Translate(FileDocument doc, Func<string, string> translate)
    {
        foreach (var segment in doc.Blocks.Where(b => !b.Verbatim).SelectMany(b => b.Segments))
        {
            segment.Target = translate(segment.Source);
            segment.Status = SegmentStatus.Confirmed;
        }
    }

    [Fact]
    public void UntranslatedExportIsByteIdentical()
    {
        var (file, doc) = Open(Samples.AllKinds);
        var result = Exporter.Export(file, doc, "zh");
        Assert.Equal(Samples.AllKinds, result.Text);
        Assert.Equal(doc.Blocks.Where(b => !b.Verbatim).Sum(b => b.Segments.Count), result.UntranslatedCount);
    }

    [Fact]
    public void UntranslatedExportIsByteIdenticalWithWindowsLineEndings()
    {
        var crlf = Samples.AllKinds.Replace("\n", "\r\n");
        var (file, doc) = Open(crlf);
        Assert.Equal(crlf, Exporter.Export(file, doc, "zh").Text);
    }

    [Fact]
    public void ChineseSentencesJoinWithoutSpaceAndSkeletonStays()
    {
        var md = "# Title\n\n- One. Two.\n  - Nested.\n\n```js\nx. y\n```\n\n| A. | B |\n| --- | --- |\n";
        var (file, doc) = Open(md);
        Translate(doc, s => s switch
        {
            "Title" => "标题",
            "One." => "一。",
            "Two." => "二。",
            "Nested." => "嵌套。",
            "A." => "甲。",
            "B" => "乙",
            _ => s,
        });

        var result = Exporter.Export(file, doc, "zh");
        Assert.Equal("# 标题\n\n- 一。二。\n  - 嵌套。\n\n```js\nx. y\n```\n\n| 甲。 | 乙 |\n| --- | --- |\n", result.Text);
        Assert.Equal(0, result.UntranslatedCount);
    }

    [Fact]
    public void EnglishTargetJoinsWithSpace()
    {
        var (file, doc) = Open("这是一。这是二。\n");
        Translate(doc, s => s == "这是一。" ? "This is one." : "This is two.");
        Assert.Equal("This is one. This is two.\n", Exporter.Export(file, doc, "en").Text);
    }

    [Fact]
    public void PartialTranslationKeepsOriginalWhitespace()
    {
        var (file, doc) = Open("One. Two.  Three.\n");
        var segments = doc.AllSegments.ToList();
        segments[0].Target = "一。";
        segments[0].Status = SegmentStatus.Draft;

        var result = Exporter.Export(file, doc, "zh");
        Assert.Equal("一。 Two.  Three.\n", result.Text);
        Assert.Equal(2, result.UntranslatedCount);
    }

    [Fact]
    public void MultiLineQuoteKeepsPrefixes()
    {
        var (file, doc) = Open("> One\n> two.\n");
        var segment = doc.AllSegments.Single();
        segment.Target = "第一行\n第二行\n第三行";
        segment.Status = SegmentStatus.Confirmed;
        Assert.Equal("> 第一行\n> 第二行\n> 第三行\n", Exporter.Export(file, doc, "zh").Text);
    }

    [Fact]
    public void NewlinesInListItemUseIndentation()
    {
        var (file, doc) = Open("- One.\n");
        var segment = doc.AllSegments.Single();
        segment.Target = "一\n二";
        segment.Status = SegmentStatus.Confirmed;
        Assert.Equal("- 一\n  二\n", Exporter.Export(file, doc, "zh").Text);
    }

    [Fact]
    public void HeadingsAndCellsStayOnOneLineAndCellsEscapePipes()
    {
        var (file, doc) = Open("# Title\n\n| A |\n| --- |\n");
        var segments = doc.AllSegments.ToList();
        segments[0].Target = "标\n题";
        segments[0].Status = SegmentStatus.Confirmed;
        segments[1].Target = "甲|乙 \\| 丙";
        segments[1].Status = SegmentStatus.Confirmed;
        Assert.Equal("# 标 题\n\n| 甲\\|乙 \\| 丙 |\n| --- |\n", Exporter.Export(file, doc, "zh").Text);
    }

    [Fact]
    public void EditedVerbatimBlockIsWrittenAsIs()
    {
        var (file, doc) = Open("```\n# comment\n```\n");
        doc.AllSegments.Single().Target = "```\n# 注释\n```";
        Assert.Equal("```\n# 注释\n```\n", Exporter.Export(file, doc, "zh").Text);
    }

    [Fact]
    public void MismatchedDocumentThrows()
    {
        var (file, doc) = Open("One.\n");
        doc.Blocks.Clear();
        Assert.Throws<InvalidOperationException>(() => Exporter.Export(file, doc, "zh"));
    }
}
