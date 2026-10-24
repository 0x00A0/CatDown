using CatDown.Core.Segmentation;

namespace CatDown.Core.Tests.Segmentation;

public class RuleSentenceSplitterTests
{
    static List<string> Split(string text, string language = "en", params string[] extraAbbreviations)
    {
        var block = BlockParser.Parse(text).Blocks.Single();
        var splitter = new RuleSentenceSplitter(extraAbbreviations);
        return splitter.Split(block.Text, block.Protected, language)
            .Select(s => block.Text.Substring(s.Start, s.Length))
            .ToList();
    }

    [Fact]
    public void SplitsPlainSentences() =>
        Assert.Equal(["Install the CLI.", "Then run it!", "Done?"], Split("Install the CLI. Then run it! Done?"));

    [Fact]
    public void KeepsClosingQuotesWithTheSentence() =>
        Assert.Equal(["He said \"stop.\"", "Then (yes.)", "Next."], Split("He said \"stop.\" Then (yes.) Next."));

    [Fact]
    public void RepeatedEndMarks() =>
        Assert.Equal(["Really?!", "Wait...", "Ok."], Split("Really?! Wait... Ok."));

    [Fact]
    public void DoesNotSplitAfterAbbreviations() =>
        Assert.Equal(["Use tools, e.g. Git or Mr. Smith's script.", "Next."], Split("Use tools, e.g. Git or Mr. Smith's script. Next."));

    [Fact]
    public void DoesNotSplitAfterInitials() =>
        Assert.Equal(["Written by J. K. Rowling.", "Fine."], Split("Written by J. K. Rowling. Fine."));

    [Fact]
    public void SwedishAbbreviations() =>
        Assert.Equal(["Vi har t.ex. Git och bl.a. Svn.", "Klart."], Split("Vi har t.ex. Git och bl.a. Svn. Klart.", "sv"));

    [Fact]
    public void ProjectAbbreviations()
    {
        Assert.Equal(["Approx. Ten.", "Ok."], Split("Approx. Ten. Ok."));
        Assert.Equal(["See Sec. Two.", "Ok."], Split("See Sec. Two. Ok.", "en", "Sec"));
    }

    [Fact]
    public void VersionNumbersAndLowercaseDoNotSplit() =>
        Assert.Equal(["Use v1.2 now. then continue.", "Next."], Split("Use v1.2 now. then continue. Next."));

    [Fact]
    public void ProtectsInlineCode() =>
        Assert.Equal(["Call `a. B()` first.", "Then go."], Split("Call `a. B()` first. Then go."));

    [Fact]
    public void ProtectsLinks() =>
        Assert.Equal(["See [the docs. Here](https://x.io/a.b) now.", "Ok."], Split("See [the docs. Here](https://x.io/a.b) now. Ok."));

    [Fact]
    public void ProtectsEmphasis() =>
        Assert.Equal(["**First. Second.** Third.", "Fourth."], Split("**First. Second.** Third. Fourth."));

    [Fact]
    public void SplitsBeforeMarkup() =>
        Assert.Equal(["One.", "**Two** here.", "`three` too."], Split("One. **Two** here. `three` too."));

    [Fact]
    public void ChineseSentences() =>
        Assert.Equal(["这是第一句。", "第二句呢？", "第三句！"], Split("这是第一句。第二句呢？第三句！", "zh"));

    [Fact]
    public void ChineseWithClosingQuote() =>
        Assert.Equal(["他说：“好的。”", "然后走了。"], Split("他说：“好的。”然后走了。", "zh"));

    [Fact]
    public void MixedChineseAndEnglish() =>
        Assert.Equal(["Run the CLI.", "然后看结果。", "Done."], Split("Run the CLI. 然后看结果。Done.", "zh"));

    [Fact]
    public void SoftLineBreaksStayInsideSentence() =>
        Assert.Equal(["One sentence\nover two lines.", "Next."], Split("One sentence\nover two lines. Next."));

    [Fact]
    public void SingleSentenceAndTrailingText()
    {
        Assert.Equal(["Only one"], Split("Only one"));
        Assert.Equal(["Ok.", "And b"], Split("Ok. And b"));
    }

    [Fact]
    public void EmptyText() =>
        Assert.Empty(new RuleSentenceSplitter().Split("", [], "en"));
}
