using CatDown.Core.Model;
using CatDown.Core.Segmentation;

namespace CatDown.Core.Tests.Segmentation;

public class BlockParserTests
{
    static List<(BlockKind Kind, string Text)> Blocks(string markdown) =>
        BlockParser.Parse(markdown).Blocks.Select(b => (b.Kind, b.Text)).ToList();

    [Fact]
    public void HeadingsAndParagraphs()
    {
        var md = "# Getting started #\n\nFirst paragraph.\nSecond line.\n\nSetext title\n============\n";
        Assert.Equal(
            [
                (BlockKind.Heading, "Getting started"),
                (BlockKind.Paragraph, "First paragraph.\nSecond line."),
                (BlockKind.Heading, "Setext title"),
            ],
            Blocks(md));
    }

    [Fact]
    public void SingleLineBlockSpanPointsAtItsText()
    {
        var md = "## Title\n\nSome *text* here.\n";
        var file = BlockParser.Parse(md);
        foreach (var block in file.Blocks)
            Assert.Equal(block.Text, md.Substring(block.SourceSpan.Start, block.SourceSpan.Length));
    }

    [Fact]
    public void NestedListsAndQuotes()
    {
        var md = "- one\n  - two\n- three\n\n> quoted\n> more\n>\n> - item in quote\n\n1. first\n2. second\n";
        Assert.Equal(
            [
                (BlockKind.Paragraph, "one"),
                (BlockKind.Paragraph, "two"),
                (BlockKind.Paragraph, "three"),
                (BlockKind.Paragraph, "quoted\nmore"),
                (BlockKind.Paragraph, "item in quote"),
                (BlockKind.Paragraph, "first"),
                (BlockKind.Paragraph, "second"),
            ],
            Blocks(md));
    }

    [Fact]
    public void QuoteKeepsContinuationPrefix()
    {
        var file = BlockParser.Parse("> quoted\n> more\n");
        var block = Assert.Single(file.Blocks);
        Assert.Equal(["> "], block.ContinuationPrefixes);
        Assert.Equal("> ", block.DefaultContinuationPrefix);
    }

    [Fact]
    public void ListItemDefaultPrefixIsIndentation()
    {
        var file = BlockParser.Parse("- item\n");
        Assert.Equal("  ", Assert.Single(file.Blocks).DefaultContinuationPrefix);
    }

    [Fact]
    public void TaskListCheckboxIsNotText()
    {
        Assert.Equal(
            [(BlockKind.Paragraph, "todo item"), (BlockKind.Paragraph, "done item")],
            Blocks("- [ ] todo item\n- [x] done item\n"));
    }

    [Fact]
    public void TableCellsAreBlocks()
    {
        var md = "| Name | Note |\n| --- | --- |\n| `a.b` | x \\| y |\n|  | empty |\n";
        Assert.Equal(
            [
                (BlockKind.TableCell, "Name"),
                (BlockKind.TableCell, "Note"),
                (BlockKind.TableCell, "`a.b`"),
                (BlockKind.TableCell, "x \\| y"),
                (BlockKind.TableCell, "empty"),
            ],
            Blocks(md));
    }

    [Fact]
    public void VerbatimBlocks()
    {
        var md = "---\ntitle: Hi\n---\n\n```bash\nnpm i -g foo\n```\n\n    indented code\n\n<div>\nhtml\n</div>\n";
        var file = BlockParser.Parse(md);
        Assert.All(file.Blocks, b => Assert.True(b.Verbatim));
        Assert.Equal(
            [
                (BlockKind.FrontMatter, "---\ntitle: Hi\n---"),
                (BlockKind.CodeBlock, "```bash\nnpm i -g foo\n```"),
                (BlockKind.CodeBlock, "    indented code"),
                (BlockKind.Html, "<div>\nhtml\n</div>"),
            ],
            file.Blocks.Select(b => (b.Kind, b.Text)).ToList());
    }

    [Theory]
    [InlineData("Text.\n\n```\ncode\n```")]
    [InlineData("Text.\n\n```\ncode")]
    [InlineData("<!--\ncomment\n-->\n```")]
    public void CodeBlockAtEndOfFileWithoutNewline(string markdown)
    {
        var file = BlockParser.Parse(markdown);
        var last = file.Blocks[^1];
        Assert.True(last.Verbatim);
        Assert.True(last.SourceSpan.End <= markdown.Length);
    }

    [Fact]
    public void ThematicBreaksAndLinkDefinitionsAreSkeleton()
    {
        Assert.Equal(
            [(BlockKind.Paragraph, "See [docs][d].")],
            Blocks("***\n\nSee [docs][d].\n\n[d]: https://example.com\n"));
    }

    [Fact]
    public void WindowsLineEndings()
    {
        var md = "# Title\r\n\r\nLine one\r\nline two\r\n\r\n```\r\ncode\r\n```\r\n";
        var file = BlockParser.Parse(md);
        Assert.Equal("\r\n", file.NewLine);
        Assert.Equal(
            [
                (BlockKind.Heading, "Title"),
                (BlockKind.Paragraph, "Line one\nline two"),
                (BlockKind.CodeBlock, "```\r\ncode\r\n```"),
            ],
            file.Blocks.Select(b => (b.Kind, b.Text)).ToList());
    }

    [Fact]
    public void InlineMarkupIsProtected()
    {
        var text = "Run `a.b()` or see [the docs. here](https://x.io/a.b) and **First. Second.** end.";
        var block = Assert.Single(BlockParser.Parse(text).Blocks);
        var protectedTexts = block.Protected.Select(s => block.Text.Substring(s.Start, s.Length)).ToList();
        Assert.Contains("`a.b()`", protectedTexts);
        Assert.Contains("[the docs. here](https://x.io/a.b)", protectedTexts);
        Assert.Contains("**First. Second.**", protectedTexts);
    }

    [Fact]
    public void ProtectedSpansAcrossLinesMapToLogicalText()
    {
        var block = Assert.Single(BlockParser.Parse("> quote with *emphasis\n> across lines*.\n").Blocks);
        var span = Assert.Single(block.Protected);
        Assert.Equal("*emphasis\nacross lines*", block.Text.Substring(span.Start, span.Length));
    }
}
