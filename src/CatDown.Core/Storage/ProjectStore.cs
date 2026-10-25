using System.Text;
using System.Text.Json;
using CatDown.Core.Model;

namespace CatDown.Core.Storage;

public enum FileLoadStatus
{
    Missing,
    Loaded,
    Broken,
}

public sealed record FileLoadResult(FileLoadStatus Status, FileDocument? Document = null, string? BackupPath = null);

public sealed record SourceText(string Text, bool HasBom);

public sealed class ProjectStore(string root)
{
    public const string DataFolderName = ".catdown";
    static readonly string[] SkippedFolders = [DataFolderName, ".git", "node_modules"];
    static readonly UTF8Encoding Utf8NoBom = new(false);

    public string Root { get; } = Path.GetFullPath(root);
    public string DataFolder => Path.Combine(Root, DataFolderName);
    string SettingsPath => Path.Combine(DataFolder, "project.json");

    public ProjectSettings? LoadSettings() =>
        File.Exists(SettingsPath) ? CatDownJson.Deserialize<ProjectSettings>(File.ReadAllText(SettingsPath)) : null;

    public void SaveSettings(ProjectSettings settings) => WriteJson(SettingsPath, settings);

    string GlossaryPath => Path.Combine(DataFolder, "glossary.json");

    public Glossary.Glossary LoadGlossary()
    {
        if (!File.Exists(GlossaryPath))
            return new Glossary.Glossary();
        try
        {
            return CatDownJson.Deserialize<Glossary.Glossary>(File.ReadAllText(GlossaryPath));
        }
        catch (JsonException)
        {
            return new Glossary.Glossary();
        }
    }

    public void SaveGlossary(Glossary.Glossary glossary) => WriteJson(GlossaryPath, glossary);

    // Relative paths with '/' separators, sorted.
    public IReadOnlyList<string> ListMarkdownFiles(ProjectSettings? settings)
    {
        var output = settings is null ? null : ResolveOutputFolder(settings);
        var files = new List<string>();
        Collect(Root, output, files);
        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    void Collect(string folder, string? output, List<string> files)
    {
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var extension = Path.GetExtension(file);
            if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase) || extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase))
                files.Add(ToRelative(file));
        }
        foreach (var sub in Directory.EnumerateDirectories(folder))
        {
            var name = Path.GetFileName(sub);
            if (SkippedFolders.Contains(name, StringComparer.OrdinalIgnoreCase))
                continue;
            if (output is not null && PathsEqual(sub, output))
                continue;
            Collect(sub, output, files);
        }
    }

    public string? ResolveOutputFolder(ProjectSettings settings) =>
        settings.OutputFolder is { Length: > 0 } folder ? Path.GetFullPath(Path.Combine(Root, folder)) : null;

    public string ToRelative(string fullPath) => Path.GetRelativePath(Root, fullPath).Replace('\\', '/');

    public string ToFull(string relativePath) => Path.GetFullPath(Path.Combine(Root, relativePath));

    string DocumentPath(string relativePath) => Path.Combine(DataFolder, "files", relativePath + ".json");

    public FileLoadResult LoadFile(string relativePath)
    {
        var path = DocumentPath(relativePath);
        if (!File.Exists(path))
            return new FileLoadResult(FileLoadStatus.Missing);
        try
        {
            var document = CatDownJson.Deserialize<FileDocument>(File.ReadAllText(path));
            if (document.Version > FileDocument.CurrentVersion)
                throw new JsonException($"Unsupported version {document.Version}.");
            return new FileLoadResult(FileLoadStatus.Loaded, document);
        }
        catch (JsonException)
        {
            var backup = Path.ChangeExtension(path, null) + $".broken-{DateTime.Now:yyyyMMddHHmmss}.json";
            File.Move(path, backup, overwrite: true);
            return new FileLoadResult(FileLoadStatus.Broken, BackupPath: backup);
        }
    }

    public void SaveFile(FileDocument document) => WriteJson(DocumentPath(document.SourcePath), document);

    // Confirmed share of translatable segments in 0..100, or null when the file has no readable
    // translation data yet. Unlike LoadFile, never touches a broken file.
    public int? ReadProgress(string relativePath)
    {
        try
        {
            var path = DocumentPath(relativePath);
            return File.Exists(path) ? Progress(CatDownJson.Deserialize<FileDocument>(File.ReadAllText(path))) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static int Progress(FileDocument document)
    {
        var segments = document.Blocks.Where(b => !b.Verbatim).SelectMany(b => b.Segments).ToList();
        return segments.Count == 0 ? 100 : segments.Count(s => s.Status == SegmentStatus.Confirmed) * 100 / segments.Count;
    }

    public SourceText ReadSource(string relativePath)
    {
        var bytes = File.ReadAllBytes(ToFull(relativePath));
        var hasBom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        return new SourceText(Utf8NoBom.GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0)), hasBom);
    }

    public void WriteSource(string relativePath, SourceText source) => WriteText(ToFull(relativePath), source);

    public static void WriteText(string path, SourceText source) =>
        AtomicWrite(path, stream =>
        {
            if (source.HasBom)
                stream.Write(Encoding.UTF8.Preamble);
            stream.Write(Utf8NoBom.GetBytes(source.Text));
        });

    static void WriteJson<T>(string path, T value)
    {
        var json = CatDownJson.Serialize(value) + "\n";
        AtomicWrite(path, stream => stream.Write(Utf8NoBom.GetBytes(json)));
    }

    // Writes to a temporary file next to the target, then renames it over the target,
    // so a crash halfway never leaves a half-written file behind.
    internal static void AtomicWrite(string path, Action<Stream> write)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + $".tmp-{Guid.NewGuid():N}";
        try
        {
            using (var stream = File.Create(temp))
                write(stream);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    static bool PathsEqual(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
