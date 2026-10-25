using System.Text;

namespace CatDown.Core.Glossary;

// Glossary as CSV with the columns source, target, note (RFC 4180 quoting, UTF-8).
public static class GlossaryCsv
{
    public static string Write(Glossary glossary)
    {
        var text = new StringBuilder("source,target,note\n");
        foreach (var entry in glossary.Entries)
            text.Append(Field(entry.Source)).Append(',').Append(Field(entry.Target)).Append(',').Append(Field(entry.Note)).Append('\n');
        return text.ToString();
    }

    static string Field(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    public static List<GlossaryEntry> Read(string csv)
    {
        var rows = Rows(csv.TrimStart('﻿'));
        if (rows.Count > 0 && rows[0].Count >= 2
            && rows[0][0].Trim().Equals("source", StringComparison.OrdinalIgnoreCase)
            && rows[0][1].Trim().Equals("target", StringComparison.OrdinalIgnoreCase))
            rows.RemoveAt(0);

        return rows
            .Where(r => r.Count > 0 && r[0].Trim().Length > 0)
            .Select(r => new GlossaryEntry
            {
                Source = r[0].Trim(),
                Target = r.Count > 1 ? r[1].Trim() : "",
                Note = r.Count > 2 ? r[2].Trim() : "",
            })
            .ToList();
    }

    // Appends entries that are not there yet (same term and translation); returns how many were added.
    public static int Merge(Glossary glossary, IEnumerable<GlossaryEntry> entries)
    {
        var added = 0;
        foreach (var entry in entries)
        {
            if (glossary.Entries.Any(e => e.Source.Equals(entry.Source, StringComparison.OrdinalIgnoreCase) && e.Target == entry.Target))
                continue;
            glossary.Entries.Add(entry);
            added++;
        }
        return added;
    }

    static List<List<string>> Rows(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = [];
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
