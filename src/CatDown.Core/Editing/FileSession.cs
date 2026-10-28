using CatDown.Core.Alignment;
using CatDown.Core.Export;
using CatDown.Core.Model;
using CatDown.Core.Segmentation;
using CatDown.Core.Storage;

namespace CatDown.Core.Editing;

// One source file opened for translation: its text, parse, and translation document kept in step.
public sealed class FileSession
{
    readonly ProjectStore _store;
    readonly ProjectSettings _settings;
    readonly DocumentBuilder _builder;

    public string RelativePath { get; }
    public SourceText Source { get; private set; }
    public ParsedFile Parsed { get; private set; }
    public FileDocument Document { get; private set; }

    // Set when the translation file was unreadable and had to be backed up.
    public string? BrokenBackupPath { get; }

    FileSession(ProjectStore store, ProjectSettings settings, string relativePath, SourceText source, ParsedFile parsed, FileDocument document, string? brokenBackup)
    {
        _store = store;
        _settings = settings;
        _builder = CreateBuilder(store, settings);
        RelativePath = relativePath;
        Source = source;
        Parsed = parsed;
        Document = document;
        BrokenBackupPath = brokenBackup;
    }

    public static DocumentBuilder CreateBuilder(ProjectStore store, ProjectSettings settings) =>
        new(CreateSplitter(store, settings), settings.SourceLanguage);

    // The project's SRX rules if it has any (with the built-in rules for languages they skip),
    // otherwise the built-in rules.
    public static ISentenceSplitter CreateSplitter(ProjectStore store, ProjectSettings settings)
    {
        var rules = new RuleSentenceSplitter(settings.Abbreviations);
        return store.LoadSrx() is { } srx ? new SrxSentenceSplitter(srx, rules) : rules;
    }

    // Opens the file and brings its translation document up to date with the source.
    public static (FileSession Session, AlignmentStats? Changes) Open(ProjectStore store, ProjectSettings settings, string relativePath)
    {
        var source = store.ReadSource(relativePath);
        var parsed = BlockParser.Parse(source.Text);
        var builder = CreateBuilder(store, settings);
        var load = store.LoadFile(relativePath);

        FileDocument document;
        AlignmentStats? changes = null;
        if (load.Document is { } saved)
        {
            if (Matches(saved, parsed))
            {
                document = saved;
            }
            else
            {
                var result = new Aligner(builder).Align(saved, parsed);
                document = result.Document;
                changes = result.Stats;
                store.SaveFile(document);
            }
        }
        else
        {
            document = builder.Build(relativePath, parsed);
            store.SaveFile(document);
        }

        return (new FileSession(store, settings, relativePath, source, parsed, document, load.BackupPath), changes);
    }

    static bool Matches(FileDocument document, ParsedFile parsed) =>
        document.SourceHash == DocumentBuilder.Hash(parsed.Text)
        && document.Blocks.Count == parsed.Blocks.Count
        && document.Blocks.Zip(parsed.Blocks).All(p => p.First.Source == p.Second.Text);

    // Rereads the source; returns what changed, or null when it did not change.
    public AlignmentStats? Refresh()
    {
        var source = _store.ReadSource(RelativePath);
        if (DocumentBuilder.Hash(source.Text) == Document.SourceHash)
            return null;
        Source = source;
        return Realign();
    }

    // Replaces the whole source file.
    public AlignmentStats ReplaceSource(string text)
    {
        Source = Source with { Text = text };
        _store.WriteSource(RelativePath, Source);
        return Realign();
    }

    // Writes an edit of one block back to the source file.
    public AlignmentStats EditSourceBlock(int blockIndex, string newText)
    {
        var text = SourceEditor.ReplaceBlock(Parsed, blockIndex, newText);
        Source = Source with { Text = text };
        _store.WriteSource(RelativePath, Source);
        return Realign();
    }

    AlignmentStats Realign()
    {
        Parsed = BlockParser.Parse(Source.Text);
        var result = new Aligner(_builder).Align(Document, Parsed);
        Document = result.Document;
        Save();
        return result.Stats;
    }

    public void Save() => _store.SaveFile(Document);

    // The splitter for the project's current rules (SRX if imported, otherwise built in).
    public ISentenceSplitter CurrentSplitter() => CreateSplitter(_store, _settings);

    public ParsedBlock ParsedBlockOf(Block block) => Parsed.Blocks[Document.Blocks.IndexOf(block)];

    public ExportResult Export() => Exporter.Export(Parsed, Document, _settings.TargetLanguage);

    // Writes the translated file under the output folder, mirroring the relative path.
    public ExportResult ExportTo(string outputFolder)
    {
        var result = Export();
        ProjectStore.WriteText(Path.Combine(outputFolder, RelativePath), Source with { Text = result.Text });
        return result;
    }
}
