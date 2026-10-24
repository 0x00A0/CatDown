using CatDown.Core.Model;
using CatDown.Core.Segmentation;

namespace CatDown.Core.Glossary;

public sealed class GlossaryEntry
{
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed record GlossaryHit(TextSpan Span, GlossaryEntry Entry);

public sealed class Glossary
{
    public int Version { get; set; } = 1;
    public List<GlossaryEntry> Entries { get; set; } = [];

    // Term occurrences in text, in order and without overlaps; longer terms win.
    // Languages written with spaces match whole words, ignoring case; Chinese matches substrings.
    public IReadOnlyList<GlossaryHit> FindHits(string text, string sourceLanguage)
    {
        var wholeWords = !Languages.JoinsWithoutSpace(sourceLanguage);
        var candidates = new List<GlossaryHit>();
        foreach (var entry in Entries)
        {
            var term = entry.Source.Trim();
            if (term.Length == 0)
                continue;
            var start = 0;
            while ((start = text.IndexOf(term, start, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var end = start + term.Length;
                if (!wholeWords || (IsBoundary(text, start - 1) && IsBoundary(text, end)))
                    candidates.Add(new GlossaryHit(new TextSpan(start, term.Length), entry));
                start = end;
            }
        }

        var hits = new List<GlossaryHit>();
        foreach (var hit in candidates.OrderByDescending(h => h.Span.Length).ThenBy(h => h.Span.Start))
        {
            if (!hits.Any(h => h.Span.Start < hit.Span.End && hit.Span.Start < h.Span.End))
                hits.Add(hit);
        }
        return hits.OrderBy(h => h.Span.Start).ToList();
    }

    static bool IsBoundary(string text, int index) =>
        index < 0 || index >= text.Length || !char.IsLetterOrDigit(text[index]);
}
