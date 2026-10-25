using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CatDown.Core.Segmentation;

public sealed record SrxRule(bool Break, Regex Position);

// Segmentation rules in the SRX 2.0 format, as used by OmegaT, Okapi, and others.
public sealed class SrxDocument
{
    readonly Dictionary<string, List<SrxRule>> _rules;
    readonly List<(Regex Pattern, string RuleName)> _maps;

    SrxDocument(bool cascade, Dictionary<string, List<SrxRule>> rules, List<(Regex, string)> maps)
    {
        Cascade = cascade;
        _rules = rules;
        _maps = maps;
    }

    public bool Cascade { get; }

    public IReadOnlyCollection<string> RuleNames => _rules.Keys;

    // Throws FormatException when the file is not usable SRX.
    public static SrxDocument Parse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException e)
        {
            throw new FormatException(e.Message, e);
        }

        var root = document.Root;
        if (root?.Name.LocalName != "srx")
            throw new FormatException("Not an SRX file.");
        var header = Child(root, "header");
        var cascade = header?.Attribute("cascade")?.Value.Equals("yes", StringComparison.OrdinalIgnoreCase) ?? false;
        var body = Child(root, "body") ?? throw new FormatException("SRX file has no body.");

        var rules = new Dictionary<string, List<SrxRule>>();
        foreach (var languageRule in Descendants(body, "languagerule"))
        {
            var name = languageRule.Attribute("languagerulename")?.Value ?? "";
            var list = new List<SrxRule>();
            foreach (var rule in Children(languageRule, "rule"))
            {
                var isBreak = !(rule.Attribute("break")?.Value.Equals("no", StringComparison.OrdinalIgnoreCase) ?? false);
                var before = Child(rule, "beforebreak")?.Value ?? "";
                var after = Child(rule, "afterbreak")?.Value ?? "";
                try
                {
                    list.Add(new SrxRule(isBreak, new Regex($"(?<={before})(?={after})", RegexOptions.CultureInvariant)));
                }
                catch (ArgumentException)
                {
                    // Java-only regex syntax; skip the rule rather than the whole file.
                }
            }
            rules[name] = list;
        }

        var maps = new List<(Regex, string)>();
        foreach (var map in Descendants(body, "languagemap"))
        {
            var pattern = map.Attribute("languagepattern")?.Value ?? "";
            var name = map.Attribute("languagerulename")?.Value ?? "";
            try
            {
                maps.Add((new Regex($"^(?:{pattern})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), name));
            }
            catch (ArgumentException)
            {
            }
        }
        if (rules.Count == 0)
            throw new FormatException("SRX file has no language rules.");
        return new SrxDocument(cascade, rules, maps);
    }

    // The rules for a language code like "en" or "zh-CN", or null when no map covers it.
    public IReadOnlyList<SrxRule>? RulesFor(string language)
    {
        var codes = new[] { language, language.Split('-')[0] };
        var names = _maps.Where(m => codes.Any(c => m.Pattern.IsMatch(c))).Select(m => m.RuleName).ToList();
        if (names.Count == 0)
            return null;
        if (!Cascade)
            names = [names[0]];
        return names.SelectMany(n => _rules.GetValueOrDefault(n) ?? []).ToList();
    }

    static XElement? Child(XElement element, string name) => element.Elements().FirstOrDefault(e => e.Name.LocalName == name);
    static IEnumerable<XElement> Children(XElement element, string name) => element.Elements().Where(e => e.Name.LocalName == name);
    static IEnumerable<XElement> Descendants(XElement element, string name) => element.Descendants().Where(e => e.Name.LocalName == name);
}

// Splits with SRX rules; languages the SRX file does not cover use the fallback splitter.
public sealed class SrxSentenceSplitter(SrxDocument srx, ISentenceSplitter fallback) : ISentenceSplitter
{
    public IReadOnlyList<TextSpan> Split(string text, IReadOnlyList<TextSpan> protectedSpans, string language)
    {
        if (srx.RulesFor(language) is not { } rules)
            return fallback.Split(text, protectedSpans, language);

        // At each position the first rule (in file order) that matches decides.
        var decisions = new Dictionary<int, bool>();
        foreach (var rule in rules)
        {
            foreach (Match match in rule.Position.Matches(text))
            {
                if (match.Index > 0 && match.Index < text.Length)
                    decisions.TryAdd(match.Index, rule.Break);
            }
        }

        var breaks = decisions.Where(d => d.Value).Select(d => d.Key)
            .Where(p => !protectedSpans.Any(s => s.Start < p && p < s.End))
            .Order();

        var sentences = new List<TextSpan>();
        var start = SkipWhitespace(text, 0);
        foreach (var position in breaks)
        {
            if (position <= start)
                continue;
            AddTrimmed(sentences, text, start, position);
            start = SkipWhitespace(text, position);
        }
        AddTrimmed(sentences, text, start, text.Length);
        return sentences;
    }

    static int SkipWhitespace(string text, int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
            i++;
        return i;
    }

    static void AddTrimmed(List<TextSpan> sentences, string text, int start, int end)
    {
        while (end > start && char.IsWhiteSpace(text[end - 1]))
            end--;
        if (end > start)
            sentences.Add(TextSpan.FromBounds(start, end));
    }
}
