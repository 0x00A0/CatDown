using System.Text;
using CatDown.Core.Glossary;
using CatDown.Core.Memory;
using GlossaryModel = CatDown.Core.Glossary.Glossary;

namespace CatDown.Core.Tests.Memory;

public sealed class ExchangeFormatTests : IDisposable
{
    readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    TranslationMemory NewMemory(string name) => new(Path.Combine(_temp.Path, name + ".sqlite"));

    static MemoryStream Stream(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public void TmxRoundTrip()
    {
        using var source = NewMemory("a");
        source.Add("en", "zh", "Install <the> CLI & tool.", "安装 CLI 和工具。");
        source.Add("en", "sv", "Hello.", "Hej.");
        var file = new MemoryStream();
        Tmx.Export(source, file);

        using var target = NewMemory("b");
        var added = Tmx.Import(target, new MemoryStream(file.ToArray()));

        Assert.Equal(2, added);
        Assert.Equal(source.All().ToList(), target.All().ToList());
        Assert.Contains("srclang=\"en\"", Encoding.UTF8.GetString(file.ToArray()));
    }

    [Fact]
    public void ImportsInlineTagsAsTextAndManyLanguages()
    {
        const string tmx = """
            <?xml version="1.0" encoding="UTF-8"?>
            <tmx version="1.4">
              <header srclang="en-US" datatype="plaintext" segtype="sentence" adminlang="en" o-tmf="x" creationtool="x" creationtoolversion="1" />
              <body>
                <tu>
                  <tuv xml:lang="en-US"><seg>Click <bpt i="1">&lt;b&gt;</bpt>Save<ept i="1">&lt;/b&gt;</ept> now.</seg></tuv>
                  <tuv xml:lang="zh-CN"><seg>立即点击<bpt i="1">&lt;b&gt;</bpt>保存<ept i="1">&lt;/b&gt;</ept>。</seg></tuv>
                  <tuv xml:lang="sv-SE"><seg>Klicka på <hi>Spara</hi> nu.</seg></tuv>
                </tu>
                <tu><tuv xml:lang="en-US"><seg>Lonely.</seg></tuv></tu>
              </body>
            </tmx>
            """;
        using var memory = NewMemory("c");

        Assert.Equal(2, Tmx.Import(memory, Stream(tmx)));
        Assert.Equal("立即点击保存。", memory.Search("en", "zh", "Click Save now.").Single().Target);
        Assert.Equal("Klicka på Spara nu.", memory.Search("en", "sv", "Click Save now.").Single().Target);
    }

    [Fact]
    public void BrokenTmxIsRejected()
    {
        using var memory = NewMemory("d");
        Assert.Throws<FormatException>(() => Tmx.Import(memory, Stream("<tmx>")));
        Assert.Throws<FormatException>(() => Tmx.Import(memory, Stream("<other />")));
    }

    [Fact]
    public void CsvRoundTripWithQuotes()
    {
        var glossary = new GlossaryModel
        {
            Entries =
            [
                new GlossaryEntry { Source = "pull request", Target = "拉取请求", Note = "" },
                new GlossaryEntry { Source = "a, b", Target = "say \"hi\"", Note = "line one\nline two" },
            ],
        };

        var csv = GlossaryCsv.Write(glossary);
        var back = GlossaryCsv.Read(csv);

        Assert.StartsWith("source,target,note\n", csv);
        Assert.Equal(glossary.Entries.Select(e => (e.Source, e.Target, e.Note)), back.Select(e => (e.Source, e.Target, e.Note)));
    }

    [Fact]
    public void CsvWithoutHeaderAndShortRows()
    {
        var entries = GlossaryCsv.Read("﻿API,接口\r\nCLI\r\n,orphan\r\n");
        Assert.Equal([("API", "接口", ""), ("CLI", "", "")], entries.Select(e => (e.Source, e.Target, e.Note)));
    }

    [Fact]
    public void MergeSkipsDuplicates()
    {
        var glossary = new GlossaryModel { Entries = [new GlossaryEntry { Source = "API", Target = "接口" }] };
        var added = GlossaryCsv.Merge(glossary, [new GlossaryEntry { Source = "api", Target = "接口" }, new GlossaryEntry { Source = "API", Target = "应用接口" }]);
        Assert.Equal(1, added);
        Assert.Equal(2, glossary.Entries.Count);
    }
}
