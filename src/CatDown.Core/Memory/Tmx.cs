using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CatDown.Core.Memory;

// TMX 1.4 import and export, the exchange format every CAT tool reads.
public static class Tmx
{
    static readonly XNamespace Xml = XNamespace.Xml;
    static readonly HashSet<string> NativeCodeTags = ["bpt", "ept", "ph", "it", "ut"];

    public static void Export(TranslationMemory memory, Stream output)
    {
        var units = memory.All().ToList();
        var sourceLanguages = units.Select(u => u.SourceLanguage).Distinct().ToList();
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("tmx", new XAttribute("version", "1.4"),
                new XElement("header",
                    new XAttribute("creationtool", "CatDown"),
                    new XAttribute("creationtoolversion", "1"),
                    new XAttribute("segtype", "sentence"),
                    new XAttribute("o-tmf", "CatDown"),
                    new XAttribute("adminlang", "en"),
                    new XAttribute("srclang", sourceLanguages.Count == 1 ? sourceLanguages[0] : "*all*"),
                    new XAttribute("datatype", "plaintext")),
                new XElement("body", units.Select(u =>
                    new XElement("tu", new XAttribute("srclang", u.SourceLanguage),
                        Variant(u.SourceLanguage, u.Source),
                        Variant(u.TargetLanguage, u.Target))))));

        using var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true });
        document.Save(writer);
    }

    static XElement Variant(string language, string text) =>
        new("tuv", new XAttribute(Xml + "lang", language), new XElement("seg", text));

    // Adds every source/target pair in the file; returns how many pairs were added.
    public static int Import(TranslationMemory memory, Stream input)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(input);
        }
        catch (XmlException e)
        {
            throw new FormatException(e.Message, e);
        }
        if (document.Root?.Name.LocalName != "tmx")
            throw new FormatException("Not a TMX file.");

        var headerSource = document.Root.Element("header")?.Attribute("srclang")?.Value;
        var units = new List<TmUnit>();
        foreach (var tu in document.Descendants("tu"))
        {
            var variants = tu.Elements("tuv")
                .Select(v => (Language: (v.Attribute(Xml + "lang") ?? v.Attribute("lang"))?.Value ?? "", Text: SegmentText(v.Element("seg"))))
                .Where(v => v.Language.Length > 0)
                .ToList();
            if (variants.Count < 2)
                continue;

            var sourceLanguage = tu.Attribute("srclang")?.Value ?? headerSource;
            var source = variants.FirstOrDefault(v => string.Equals(v.Language, sourceLanguage, StringComparison.OrdinalIgnoreCase));
            if (source.Language is null || source.Language.Length == 0)
                source = variants[0];
            foreach (var target in variants.Where(v => v != source))
                units.Add(new TmUnit(source.Language, target.Language, source.Text, target.Text));
        }
        return memory.AddMany(units);
    }

    // Text of a <seg>, leaving out native codes such as <bpt>…</bpt> but keeping <hi> text.
    static string SegmentText(XElement? seg)
    {
        if (seg is null)
            return "";
        var text = new StringBuilder();
        foreach (var node in seg.Nodes())
        {
            switch (node)
            {
                case XText t:
                    text.Append(t.Value);
                    break;
                case XElement e when !NativeCodeTags.Contains(e.Name.LocalName):
                    text.Append(SegmentText(e));
                    break;
            }
        }
        return text.ToString();
    }
}
