namespace CatDown.Core.Segmentation;

public sealed class RuleSentenceSplitter : ISentenceSplitter
{
    const string CjkEnds = "。！？";
    const string LatinEnds = ".!?";
    const string Closers = "\"'”’）)」』》】";
    const string SentenceOpeners = "\"'“‘(（「『[`*_<!";

    public static readonly IReadOnlyList<string> BuiltInAbbreviations =
    [
        // English
        "mr.", "mrs.", "ms.", "dr.", "prof.", "sr.", "jr.", "st.", "vs.", "e.g.", "i.e.", "cf.",
        "approx.", "fig.", "vol.", "pp.", "inc.", "ltd.", "corp.", "dept.",
        "jan.", "feb.", "aug.", "sept.", "oct.", "nov.", "dec.",
        // Swedish
        "t.ex.", "bl.a.", "m.m.", "osv.", "dvs.", "s.k.", "ca.", "nr.", "fr.o.m.", "t.o.m.",
        "m.fl.", "resp.", "jfr.", "kl.", "f.d.", "p.g.a.",
    ];

    readonly HashSet<string> _abbreviations;

    public RuleSentenceSplitter(IEnumerable<string>? extraAbbreviations = null)
    {
        _abbreviations = new HashSet<string>(BuiltInAbbreviations, StringComparer.OrdinalIgnoreCase);
        foreach (var abbreviation in extraAbbreviations ?? [])
            _abbreviations.Add(abbreviation.Trim().EndsWith('.') ? abbreviation.Trim() : abbreviation.Trim() + ".");
    }

    public IReadOnlyList<TextSpan> Split(string text, IReadOnlyList<TextSpan> protectedSpans, string language)
    {
        var sentences = new List<TextSpan>();
        var start = SkipWhitespace(text, 0);
        var i = start;
        while (i < text.Length)
        {
            var c = text[i];
            if (!IsEnd(c) || IsProtected(protectedSpans, i))
            {
                i++;
                continue;
            }

            var j = i + 1;
            while (j < text.Length && IsEnd(text[j]) && !IsProtected(protectedSpans, j))
                j++;
            while (j < text.Length && Closers.Contains(text[j]))
                j++;

            if (j < text.Length && BreaksAfter(text, i, j))
            {
                sentences.Add(TrimEnd(text, start, j));
                start = SkipWhitespace(text, j);
                i = start;
            }
            else
            {
                i = j;
            }
        }

        if (start < text.Length)
            sentences.Add(TrimEnd(text, start, text.Length));
        return sentences;
    }

    bool BreaksAfter(string text, int endStart, int endStop)
    {
        for (var k = endStart; k < endStop; k++)
        {
            if (CjkEnds.Contains(text[k]))
                return true;
        }

        if (!char.IsWhiteSpace(text[endStop]))
            return false;
        var next = SkipWhitespace(text, endStop);
        if (next >= text.Length || !StartsSentence(text[next]))
            return false;

        var singleDot = text[endStart] == '.' && (endStop - endStart == 1 || !IsEnd(text[endStart + 1]));
        return !(singleDot && IsAbbreviation(text, endStart));
    }

    bool IsAbbreviation(string text, int dot)
    {
        var tokenStart = dot;
        while (tokenStart > 0 && !char.IsWhiteSpace(text[tokenStart - 1]) && text[tokenStart - 1] is not ('(' or '（'))
            tokenStart--;
        var token = text.Substring(tokenStart, dot - tokenStart + 1);
        if (token.Length == 2 && char.IsLetter(token[0]))
            return true; // an initial, as in "J. K. Rowling"
        return _abbreviations.Contains(token);
    }

    static bool IsEnd(char c) => CjkEnds.Contains(c) || LatinEnds.Contains(c);

    static bool StartsSentence(char c) =>
        char.IsUpper(c) || char.IsDigit(c) || IsCjk(c) || SentenceOpeners.Contains(c);

    static bool IsCjk(char c) =>
        c is >= '一' and <= '鿿' or >= '㐀' and <= '䶿' or >= '぀' and <= 'ヿ' or >= '가' and <= '힯';

    static bool IsProtected(IReadOnlyList<TextSpan> spans, int offset)
    {
        foreach (var span in spans)
        {
            if (span.Contains(offset))
                return true;
        }
        return false;
    }

    static int SkipWhitespace(string text, int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
            i++;
        return i;
    }

    static TextSpan TrimEnd(string text, int start, int end)
    {
        while (end > start && char.IsWhiteSpace(text[end - 1]))
            end--;
        return TextSpan.FromBounds(start, end);
    }
}
