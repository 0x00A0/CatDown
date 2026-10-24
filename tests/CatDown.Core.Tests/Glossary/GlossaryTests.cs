using CatDown.Core.Glossary;
using CatDown.Core.Storage;
using GlossaryModel = CatDown.Core.Glossary.Glossary;

namespace CatDown.Core.Tests.Glossary;

public class GlossaryTests
{
    static GlossaryModel Make(params string[] terms) =>
        new() { Entries = terms.Select(t => new GlossaryEntry { Source = t, Target = t + "-zh" }).ToList() };

    static List<string> Hits(GlossaryModel glossary, string text, string language = "en") =>
        glossary.FindHits(text, language).Select(h => text.Substring(h.Span.Start, h.Span.Length)).ToList();

    [Fact]
    public void MatchesWholeWordsIgnoringCase() =>
        Assert.Equal(["CLI", "cli"], Hits(Make("cli"), "The CLI and the cli, not clip or eclipse."));

    [Fact]
    public void LongerTermWins() =>
        Assert.Equal(["command line", "line"], Hits(Make("line", "command line"), "Use the command line. One line."));

    [Fact]
    public void ChineseMatchesSubstrings() =>
        Assert.Equal(["命令行", "命令行"], Hits(Make("命令行"), "在命令行里输入，命令行工具", "zh"));

    [Fact]
    public void MultiWordAndPunctuation() =>
        Assert.Equal(["pull request"], Hits(Make("pull request"), "Open a pull request."));

    [Fact]
    public void EmptyTermsAreIgnored() =>
        Assert.Empty(Hits(Make(" "), "anything"));

    [Fact]
    public void HitsCarryEntry()
    {
        var hit = Assert.Single(Make("API").FindHits("Call the API.", "en"));
        Assert.Equal("API-zh", hit.Entry.Target);
    }

    [Fact]
    public void SavedWithProject()
    {
        using var temp = new TempFolder();
        var store = new ProjectStore(temp.Path);
        Assert.Empty(store.LoadGlossary().Entries);

        var glossary = Make("CLI");
        glossary.Entries[0].Note = "不翻";
        store.SaveGlossary(glossary);

        var loaded = store.LoadGlossary();
        Assert.Equal(("CLI", "CLI-zh", "不翻"), (loaded.Entries[0].Source, loaded.Entries[0].Target, loaded.Entries[0].Note));
        Assert.True(File.Exists(Path.Combine(temp.Path, ".catdown", "glossary.json")));
    }
}
