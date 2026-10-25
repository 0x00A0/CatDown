using CatDown.Core.Editing;
using CatDown.Core.Model;
using CatDown.Core.Segmentation;
using CatDown.Core.Storage;

namespace CatDown.Core.Tests.Segmentation;

public class SrxTests
{
    const string Sample = """
        <?xml version="1.0" encoding="UTF-8"?>
        <srx xmlns="http://www.lisa.org/srx20" version="2.0">
          <header segmentsubflows="yes" cascade="no" />
          <body>
            <languagerules>
              <languagerule languagerulename="English">
                <rule break="no"><beforebreak>\bDr\.</beforebreak><afterbreak>\s</afterbreak></rule>
                <rule break="yes"><beforebreak>[\.\?!]+</beforebreak><afterbreak>\s+\p{Lu}</afterbreak></rule>
                <rule break="yes"><beforebreak>;</beforebreak><afterbreak>\s</afterbreak></rule>
              </languagerule>
              <languagerule languagerulename="Broken">
                <rule break="yes"><beforebreak>a++</beforebreak><afterbreak></afterbreak></rule>
              </languagerule>
            </languagerules>
            <maprules>
              <languagemap languagepattern="EN.*" languagerulename="English" />
            </maprules>
          </body>
        </srx>
        """;

    static List<string> Split(string markdown, string language = "en", string srx = Sample)
    {
        var block = BlockParser.Parse(markdown).Blocks.Single();
        var splitter = new SrxSentenceSplitter(SrxDocument.Parse(srx), new RuleSentenceSplitter());
        return splitter.Split(block.Text, block.Protected, language).Select(s => block.Text.Substring(s.Start, s.Length)).ToList();
    }

    [Fact]
    public void FirstMatchingRuleDecides() =>
        Assert.Equal(["Ask Dr. Smith.", "Then go;", "now Done!"], Split("Ask Dr. Smith. Then go; now Done!"));

    [Fact]
    public void SemicolonRuleFromFileApplies() =>
        Assert.Equal(["One;", "two."], Split("One; two."));

    [Fact]
    public void MarkupIsStillProtected() =>
        Assert.Equal(["Use `a. B` here.", "Next."], Split("Use `a. B` here. Next."));

    [Fact]
    public void UncoveredLanguageFallsBackToBuiltInRules() =>
        Assert.Equal(["第一句。", "第二句。"], Split("第一句。第二句。", "zh"));

    [Fact]
    public void LanguagePatternMatchesRegionCodes() =>
        Assert.Equal(["One;", "two."], Split("One; two.", "en-US"));

    [Fact]
    public void CascadeCombinesRules()
    {
        var srx = """
            <srx version="2.0"><header cascade="yes" /><body>
              <languagerules>
                <languagerule languagerulename="Comma"><rule break="yes"><beforebreak>,</beforebreak><afterbreak>\s</afterbreak></rule></languagerule>
                <languagerule languagerulename="Default"><rule break="yes"><beforebreak>\.</beforebreak><afterbreak>\s</afterbreak></rule></languagerule>
              </languagerules>
              <maprules>
                <languagemap languagepattern="en" languagerulename="Comma" />
                <languagemap languagepattern=".*" languagerulename="Default" />
              </maprules>
            </body></srx>
            """;
        Assert.Equal(["a,", "b.", "c"], Split("a, b. c", "en", srx));
        Assert.Equal(["a, b.", "c"], Split("a, b. c", "sv", srx));
    }

    [Fact]
    public void InvalidFilesAreRejected()
    {
        Assert.Throws<FormatException>(() => SrxDocument.Parse("<notsrx />"));
        Assert.Throws<FormatException>(() => SrxDocument.Parse("not xml"));
        Assert.Throws<FormatException>(() => SrxDocument.Parse("<srx><body /></srx>"));
    }

    [Fact]
    public void ProjectUsesImportedSrx()
    {
        using var temp = new TempFolder();
        var srxFile = temp.Write("rules.srx", Sample);
        temp.Write("a.md", "One; two.\n");
        var store = new ProjectStore(temp.Path);
        var settings = new ProjectSettings { SourceLanguage = "en", TargetLanguage = "zh" };

        Assert.IsType<RuleSentenceSplitter>(FileSession.CreateSplitter(store, settings));
        store.ImportSrx(srxFile);
        Assert.True(store.HasSrx);
        Assert.IsType<SrxSentenceSplitter>(FileSession.CreateSplitter(store, settings));

        var (session, _) = FileSession.Open(store, settings, "a.md");
        Assert.Equal(["One;", "two."], session.Document.AllSegments.Select(s => s.Source));

        store.RemoveSrx();
        Assert.IsType<RuleSentenceSplitter>(FileSession.CreateSplitter(store, settings));
    }

    [Fact]
    public void ImportingBrokenSrxKeepsProjectUnchanged()
    {
        using var temp = new TempFolder();
        var bad = temp.Write("bad.srx", "<srx>");
        var store = new ProjectStore(temp.Path);
        Assert.Throws<FormatException>(() => store.ImportSrx(bad));
        Assert.False(store.HasSrx);
    }
}
