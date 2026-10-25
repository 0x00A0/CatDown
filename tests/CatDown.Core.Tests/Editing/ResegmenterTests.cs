using CatDown.Core.Editing;
using CatDown.Core.Llm;
using CatDown.Core.Model;
using CatDown.Core.Segmentation;
using CatDown.Core.Storage;

namespace CatDown.Core.Tests.Editing;

public class ResegmenterTests
{
    sealed class ScriptedLlm(params string[] replies) : ILlmClient
    {
        readonly Queue<string> _replies = new(replies);
        public List<IReadOnlyList<ChatMessage>> Calls { get; } = [];

        public Task<string> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            Calls.Add(messages);
            return Task.FromResult(_replies.Dequeue());
        }
    }

    static readonly ProjectSettings Settings = new() { SourceLanguage = "en", TargetLanguage = "zh" };

    static (FileSession Session, TempFolder Temp) Open(string markdown)
    {
        var temp = new TempFolder();
        temp.Write("a.md", markdown);
        return (FileSession.Open(new ProjectStore(temp.Path), Settings, "a.md").Session, temp);
    }

    // Splits every block at commas instead of sentence ends.
    static Task<IReadOnlyList<TextSpan>> AtCommas(Block block, ParsedBlock parsed)
    {
        var spans = new List<TextSpan>();
        var start = 0;
        for (var i = 0; i < block.Source.Length; i++)
        {
            if (block.Source[i] is ',' or '.')
            {
                spans.Add(TextSpan.FromBounds(start, i + 1));
                start = i + 1;
                while (start < block.Source.Length && block.Source[start] == ' ')
                    start++;
            }
        }
        return Task.FromResult<IReadOnlyList<TextSpan>>(spans);
    }

    static List<(string Source, string Target, SegmentStatus Status)> Rows(FileSession session) =>
        session.Document.AllSegments.Select(s => (s.Source, s.Target, s.Status)).ToList();

    [Fact]
    public async Task UntranslatedBlocksAreSplitWithoutAsking()
    {
        var (session, temp) = Open("One, two.\n");
        using var _ = temp;
        var asked = 0;
        var resegmenter = new Resegmenter("zh", _ => { asked++; return Task.FromResult(TranslatedBlockAction.Skip); });

        var stats = await resegmenter.ResegmentAsync(session, session.Document.Blocks, AtCommas);

        Assert.Equal(0, asked);
        Assert.Equal(new ResegmentStats(1, 0), stats);
        Assert.Equal([("One,", "", SegmentStatus.Untranslated), ("two.", "", SegmentStatus.Untranslated)], Rows(session));
    }

    static void TranslateAll(FileSession session)
    {
        foreach (var s in session.Document.AllSegments)
        {
            s.Target = "译" + s.Source;
            s.Status = SegmentStatus.Confirmed;
        }
    }

    [Fact]
    public async Task SkipLeavesTranslatedBlock()
    {
        var (session, temp) = Open("One, two.\n");
        using var _ = temp;
        TranslateAll(session);

        var stats = await new Resegmenter("zh", _ => Task.FromResult(TranslatedBlockAction.Skip)).ResegmentAsync(session, session.Document.Blocks, AtCommas);

        Assert.Equal(new ResegmentStats(0, 1), stats);
        Assert.Equal([("One, two.", "译One, two.", SegmentStatus.Confirmed)], Rows(session));
    }

    [Fact]
    public async Task MergeIntoFirstKeepsWholeTranslation()
    {
        var (session, temp) = Open("One, two.\n");
        using var _ = temp;
        TranslateAll(session);

        await new Resegmenter("zh", _ => Task.FromResult(TranslatedBlockAction.MergeIntoFirst)).ResegmentAsync(session, session.Document.Blocks, AtCommas);

        Assert.Equal([("One,", "译One, two.", SegmentStatus.NeedsReview), ("two.", "", SegmentStatus.NeedsReview)], Rows(session));
    }

    [Fact]
    public async Task LlmRealignSplitsTranslation()
    {
        var (session, temp) = Open("One, two.\n");
        using var _ = temp;
        session.Document.AllSegments.Single().Target = "一，二。";
        session.Document.AllSegments.Single().Status = SegmentStatus.Confirmed;
        var llm = new ScriptedLlm("""["一，", "二。"]""");

        await new Resegmenter("zh", _ => Task.FromResult(TranslatedBlockAction.LlmRealign), new LlmRealigner(llm))
            .ResegmentAsync(session, session.Document.Blocks, AtCommas);

        Assert.Equal([("One,", "一，", SegmentStatus.NeedsReview), ("two.", "二。", SegmentStatus.NeedsReview)], Rows(session));
        Assert.Contains("1. One,", llm.Calls.Single()[1].Content);
    }

    [Fact]
    public async Task LlmRealignThatChangesTextFallsBackToMerge()
    {
        var (session, temp) = Open("One, two.\n");
        using var _ = temp;
        session.Document.AllSegments.Single().Target = "一，二。";
        session.Document.AllSegments.Single().Status = SegmentStatus.Confirmed;
        var llm = new ScriptedLlm("""["一，", "三。"]""");

        await new Resegmenter("zh", _ => Task.FromResult(TranslatedBlockAction.LlmRealign), new LlmRealigner(llm))
            .ResegmentAsync(session, session.Document.Blocks, AtCommas);

        Assert.Equal([("One,", "一，二。", SegmentStatus.NeedsReview), ("two.", "", SegmentStatus.NeedsReview)], Rows(session));
    }

    [Fact]
    public async Task LlmSegmenterMapsSentencesBack()
    {
        var parsed = BlockParser.Parse("Install the **CLI. Tool** now.  Then run it.\n").Blocks.Single();
        var llm = new ScriptedLlm("""
            ```json
            ["Install the **CLI. Tool** now.", "Then run it."]
            ```
            """);

        var spans = await new LlmSegmenter(llm).SplitAsync(parsed.Text, parsed.Protected, TestContext.Current.CancellationToken);

        Assert.Equal(["Install the **CLI. Tool** now.", "Then run it."], spans.Select(s => parsed.Text.Substring(s.Start, s.Length)));
    }

    [Theory]
    [InlineData("""["Install the CLI.", "Then run it now."]""", "changed the text")]
    [InlineData("no list here", "list of sentences")]
    [InlineData("""["Use **a.", "b** now."]""", "inside inline markup")]
    public async Task LlmSegmenterRejectsBadReplies(string reply, string error)
    {
        var text = reply.Contains("**a.") ? "Use **a. b** now." : "Install the CLI. Then run it.";
        var parsed = BlockParser.Parse(text).Blocks.Single();
        var exception = await Assert.ThrowsAsync<LlmException>(() =>
            new LlmSegmenter(new ScriptedLlm(reply)).SplitAsync(parsed.Text, parsed.Protected, TestContext.Current.CancellationToken));
        Assert.Contains(error, exception.Message);
    }

    [Fact]
    public async Task ResegmentingWithSameResultChangesNothing()
    {
        var (session, temp) = Open("One. Two.\n");
        using var _ = temp;
        TranslateAll(session);
        var splitter = new RuleSentenceSplitter();

        var stats = await new Resegmenter("zh", _ => throw new InvalidOperationException("should not ask"))
            .ResegmentAsync(session, session.Document.Blocks, (b, p) => Task.FromResult(splitter.Split(b.Source, p.Protected, "en")));

        Assert.Equal(new ResegmentStats(0, 0), stats);
    }
}
