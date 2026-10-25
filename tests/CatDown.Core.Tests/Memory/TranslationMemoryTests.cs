using CatDown.Core.Memory;

namespace CatDown.Core.Tests.Memory;

public sealed class TranslationMemoryTests : IDisposable
{
    readonly TempFolder _temp = new();
    readonly TranslationMemory _tm;

    public TranslationMemoryTests() => _tm = new TranslationMemory(Path.Combine(_temp.Path, "tm.sqlite"));

    public void Dispose()
    {
        _tm.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public void ExactMatchScores100AndIgnoresWhitespaceAndCase()
    {
        _tm.Add("en", "zh", "Install the CLI.", "安装 CLI。");

        var match = Assert.Single(_tm.Search("en", "zh", "  install   the CLI. "));
        Assert.Equal(new TmMatch(100, "Install the CLI.", "安装 CLI。"), match);
    }

    [Fact]
    public void SamePairIsStoredOnce()
    {
        _tm.Add("en", "zh", "A sentence.", "一句。");
        _tm.Add("en", "zh", "A sentence.", "一句。");
        _tm.Add("en", "zh", "A sentence.", "另一种译法。");
        Assert.Equal(2, _tm.Count());
    }

    [Fact]
    public void FuzzyMatchesAboveThresholdBestFirst()
    {
        _tm.Add("en", "zh", "Install the command line tool.", "安装命令行工具。");
        _tm.Add("en", "zh", "Install the command line tools.", "安装这些命令行工具。");
        _tm.Add("en", "zh", "Something completely different.", "完全不同。");

        var matches = _tm.Search("en", "zh", "Install the command line tools!");

        Assert.Equal(["安装这些命令行工具。", "安装命令行工具。"], matches.Select(m => m.Target));
        Assert.All(matches, m => Assert.InRange(m.Score, 75, 99));
        Assert.True(matches[0].Score > matches[1].Score);
    }

    [Fact]
    public void LowSimilarityIsNotReturned()
    {
        _tm.Add("en", "zh", "Open the settings window.", "打开设置窗口。");
        Assert.Empty(_tm.Search("en", "zh", "Close the main window now."));
    }

    [Fact]
    public void LanguagePairsAreSeparate()
    {
        _tm.Add("en", "zh", "Hello.", "你好。");
        _tm.Add("en", "sv", "Hello.", "Hej.");

        Assert.Equal("Hej.", Assert.Single(_tm.Search("en", "sv", "Hello.")).Target);
        Assert.Equal("你好。", Assert.Single(_tm.Search("EN", "zh-CN", "Hello.")).Target);
    }

    [Fact]
    public void ReturnsAtMostThree()
    {
        for (var i = 0; i < 5; i++)
            _tm.Add("en", "zh", "Run the tool now.", $"译法{i}");
        Assert.Equal(3, _tm.Search("en", "zh", "Run the tool now.").Count);
    }

    [Fact]
    public void EmptyTextIsIgnored()
    {
        _tm.Add("en", "zh", "  ", "x");
        _tm.Add("en", "zh", "x", "");
        Assert.Equal(0, _tm.Count());
        Assert.Empty(_tm.Search("en", "zh", " "));
    }

    [Fact]
    public void PersistsAcrossInstances()
    {
        _tm.Add("en", "zh", "Saved.", "已保存。");
        _tm.Dispose();
        using var reopened = new TranslationMemory(Path.Combine(_temp.Path, "tm.sqlite"));
        Assert.Single(reopened.Search("en", "zh", "Saved."));
    }

    [Theory]
    [InlineData("abc", "abc", 100)]
    [InlineData("abcd", "abce", 75)]
    [InlineData("abc", "xyz", 0)]
    public void SimilarityFromEditDistance(string a, string b, int expected) =>
        Assert.Equal(expected, TranslationMemory.Similarity(a, b));
}
