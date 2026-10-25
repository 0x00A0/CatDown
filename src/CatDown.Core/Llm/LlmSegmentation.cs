using System.Text;
using System.Text.Json;
using CatDown.Core.Segmentation;

namespace CatDown.Core.Llm;

static class LlmJson
{
    // The first JSON array of strings in a reply, ignoring code fences and chatter around it.
    public static List<string>? StringArray(string reply)
    {
        var start = reply.IndexOf('[');
        var end = reply.LastIndexOf(']');
        if (start < 0 || end <= start)
            return null;
        try
        {
            return JsonSerializer.Deserialize<List<string>>(reply[start..(end + 1)]);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string WithoutWhitespace(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c))
                result.Append(c);
        }
        return result.ToString();
    }
}

// Sentence splitting by a language model, checked so the text itself never changes.
public sealed class LlmSegmenter(ILlmClient client)
{
    const string Instructions = """
        Split the user's text into sentences. The text is one paragraph of a Markdown document and may contain inline markup.
        Reply with a JSON array of strings only: one sentence per item, in order.
        Copy the text exactly. Do not translate, correct, reformat, or drop anything. Never split inside inline code, links, or emphasis.
        """;

    public async Task<IReadOnlyList<TextSpan>> SplitAsync(string text, IReadOnlyList<TextSpan> protectedSpans, CancellationToken cancellationToken = default)
    {
        var reply = await client.CompleteAsync([new ChatMessage("system", Instructions), new ChatMessage("user", text)], cancellationToken);
        var sentences = LlmJson.StringArray(reply)?.Where(s => s.Trim().Length > 0).ToList()
            ?? throw new LlmException("The model did not reply with a list of sentences.");
        if (LlmJson.WithoutWhitespace(string.Concat(sentences)) != LlmJson.WithoutWhitespace(text))
            throw new LlmException("The model changed the text while splitting it.");

        var spans = MapBack(text, sentences);
        if (spans.Skip(1).Any(s => protectedSpans.Any(p => p.Start < s.Start && s.Start < p.End)))
            throw new LlmException("The model split inside inline markup.");
        return spans;
    }

    // Finds each sentence in the text by walking the non-whitespace characters in step.
    static List<TextSpan> MapBack(string text, List<string> sentences)
    {
        var spans = new List<TextSpan>();
        var i = 0;
        foreach (var sentence in sentences)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;
            var start = i;
            foreach (var c in sentence)
            {
                if (char.IsWhiteSpace(c))
                    continue;
                while (char.IsWhiteSpace(text[i]))
                    i++;
                i++;
            }
            spans.Add(TextSpan.FromBounds(start, i));
        }
        return spans;
    }
}

// Splits an existing translation to match a new sentence split of its source.
public sealed class LlmRealigner(ILlmClient client)
{
    // Null when the model's answer does not fit; callers then fall back to merging.
    public async Task<IReadOnlyList<string>?> RealignAsync(string translation, IReadOnlyList<string> sources, CancellationToken cancellationToken = default)
    {
        var user = new StringBuilder();
        user.AppendLine($"The source paragraph is now split into {sources.Count} sentences:");
        for (var i = 0; i < sources.Count; i++)
            user.AppendLine($"{i + 1}. {sources[i]}");
        user.AppendLine().AppendLine("Existing translation of the whole paragraph:").Append(translation);

        const string instructions = """
            Split the existing translation into parts that match the source sentences, one part per sentence, in order.
            Copy the translation exactly: do not change, add, or drop anything. A part may be empty if nothing in the translation matches that sentence.
            Reply with a JSON array of strings only, with exactly as many items as there are source sentences.
            """;
        try
        {
            var reply = await client.CompleteAsync([new ChatMessage("system", instructions), new ChatMessage("user", user.ToString())], cancellationToken);
            var parts = LlmJson.StringArray(reply);
            if (parts is null || parts.Count != sources.Count)
                return null;
            if (LlmJson.WithoutWhitespace(string.Concat(parts)) != LlmJson.WithoutWhitespace(translation))
                return null;
            return parts.Select(p => p.Trim()).ToList();
        }
        catch (LlmException)
        {
            return null;
        }
    }
}
