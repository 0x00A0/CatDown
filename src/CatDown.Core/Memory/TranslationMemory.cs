using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace CatDown.Core.Memory;

public sealed record TmMatch(int Score, string Source, string Target);

public sealed record TmUnit(string SourceLanguage, string TargetLanguage, string Source, string Target);

// The global translation memory: confirmed sentence pairs, searched by similarity.
public sealed class TranslationMemory : IDisposable
{
    public const int DefaultThreshold = 75;
    readonly SqliteConnection _connection;

    public TranslationMemory(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        _connection.Open();
        Execute("""
            CREATE TABLE IF NOT EXISTS units (
                id INTEGER PRIMARY KEY,
                source_lang TEXT NOT NULL,
                target_lang TEXT NOT NULL,
                source TEXT NOT NULL,
                target TEXT NOT NULL,
                normalized TEXT NOT NULL,
                len INTEGER NOT NULL,
                updated TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS units_lookup ON units (source_lang, target_lang, len);
            CREATE UNIQUE INDEX IF NOT EXISTS units_unique ON units (source_lang, target_lang, source, target);
            """);
    }

    public static string DefaultPath(string appDataFolder) => Path.Combine(appDataFolder, "tm.sqlite");

    public void Add(string sourceLanguage, string targetLanguage, string source, string target) =>
        AddMany([new TmUnit(sourceLanguage, targetLanguage, source, target)]);

    // Adds many pairs in one transaction; returns how many were usable.
    public int AddMany(IEnumerable<TmUnit> units)
    {
        using var transaction = _connection.BeginTransaction();
        var added = 0;
        foreach (var unit in units)
        {
            if (Insert(unit, transaction))
                added++;
        }
        transaction.Commit();
        return added;
    }

    public IEnumerable<TmUnit> All()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT source_lang, target_lang, source, target FROM units ORDER BY id;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            yield return new TmUnit(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
    }

    bool Insert(TmUnit unit, SqliteTransaction transaction)
    {
        var (sourceLanguage, targetLanguage, source, target) = unit;
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            return false;
        var normalized = Normalize(source);
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO units (source_lang, target_lang, source, target, normalized, len, updated)
            VALUES ($sl, $tl, $source, $target, $normalized, $len, $updated)
            ON CONFLICT (source_lang, target_lang, source, target) DO UPDATE SET updated = excluded.updated;
            """;
        command.Parameters.AddWithValue("$sl", Language(sourceLanguage));
        command.Parameters.AddWithValue("$tl", Language(targetLanguage));
        command.Parameters.AddWithValue("$source", source);
        command.Parameters.AddWithValue("$target", target);
        command.Parameters.AddWithValue("$normalized", normalized);
        command.Parameters.AddWithValue("$len", normalized.Length);
        command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
        return true;
    }

    // Best matches first. Exact matches (after normalizing whitespace and case) score 100.
    public IReadOnlyList<TmMatch> Search(string sourceLanguage, string targetLanguage, string source, int limit = 3, int threshold = DefaultThreshold)
    {
        var normalized = Normalize(source);
        if (normalized.Length == 0)
            return [];

        // Strings whose lengths differ by more than the allowed share cannot reach the threshold.
        var slack = (int)Math.Ceiling(normalized.Length * (100 - threshold) / (double)threshold);
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT source, target, normalized FROM units
            WHERE source_lang = $sl AND target_lang = $tl AND len BETWEEN $min AND $max
            ORDER BY updated DESC;
            """;
        command.Parameters.AddWithValue("$sl", Language(sourceLanguage));
        command.Parameters.AddWithValue("$tl", Language(targetLanguage));
        command.Parameters.AddWithValue("$min", normalized.Length - slack);
        command.Parameters.AddWithValue("$max", normalized.Length + slack);

        var matches = new List<TmMatch>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var score = Similarity(normalized, reader.GetString(2));
            if (score >= threshold)
                matches.Add(new TmMatch(score, reader.GetString(0), reader.GetString(1)));
        }
        // Stable sort keeps the most recently used first among equal scores.
        return matches.OrderByDescending(m => m.Score).Take(limit).ToList();
    }

    public int Count()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM units;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public static string Normalize(string text)
    {
        var result = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }
            if (space)
                result.Append(' ');
            space = false;
            result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
    }

    // 0..100, from the edit distance relative to the longer string.
    public static int Similarity(string a, string b)
    {
        if (a == b)
            return 100;
        var longer = Math.Max(a.Length, b.Length);
        if (longer == 0)
            return 100;
        var distance = EditDistance(a, b);
        // Only a true exact match may score 100.
        return Math.Min(99, (int)Math.Floor(100.0 * (longer - distance) / longer));
    }

    static int EditDistance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }

    static string Language(string code) => code.Split('-')[0].ToLowerInvariant();

    void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}
