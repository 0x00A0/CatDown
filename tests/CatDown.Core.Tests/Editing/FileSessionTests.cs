using CatDown.Core.Editing;
using CatDown.Core.Model;
using CatDown.Core.Segmentation;
using CatDown.Core.Storage;

namespace CatDown.Core.Tests.Editing;

public class FileSessionTests
{
    static readonly ProjectSettings Settings = new() { SourceLanguage = "en", TargetLanguage = "zh" };

    [Fact]
    public void ReplaceBlockChangesOnlyThatBlock()
    {
        var file = BlockParser.Parse("# Title\n\n> One\n> two.\n\nEnd.\n");
        var text = SourceEditor.ReplaceBlock(file, 1, "Changed\nquote.");
        Assert.Equal("# Title\n\n> Changed\n> quote.\n\nEnd.\n", text);
        Assert.Same(file.Text, SourceEditor.ReplaceBlock(file, 0, "Title"));
    }

    [Fact]
    public void OpeningNewFileBuildsAndSavesDocument()
    {
        using var temp = new TempFolder();
        temp.Write("a.md", "One. Two.\n");
        var store = new ProjectStore(temp.Path);

        var (session, changes) = FileSession.Open(store, Settings, "a.md");

        Assert.Null(changes);
        Assert.Equal(2, session.Document.AllSegments.Count());
        Assert.Equal(FileLoadStatus.Loaded, store.LoadFile("a.md").Status);
    }

    [Fact]
    public void ReopeningKeepsTranslations()
    {
        using var temp = new TempFolder();
        temp.Write("a.md", "One. Two.\n");
        var store = new ProjectStore(temp.Path);
        var (session, _) = FileSession.Open(store, Settings, "a.md");
        session.Document.AllSegments.First().Target = "一。";
        session.Document.AllSegments.First().Status = SegmentStatus.Confirmed;
        session.Save();

        var (reopened, changes) = FileSession.Open(store, Settings, "a.md");

        Assert.Null(changes);
        Assert.Equal("一。", reopened.Document.AllSegments.First().Target);
    }

    [Fact]
    public void ExternalChangeIsAlignedOnOpenAndRefresh()
    {
        using var temp = new TempFolder();
        temp.Write("a.md", "One. Two.\n");
        var store = new ProjectStore(temp.Path);
        var (session, _) = FileSession.Open(store, Settings, "a.md");
        foreach (var s in session.Document.AllSegments)
        {
            s.Target = s.Source + "-zh";
            s.Status = SegmentStatus.Confirmed;
        }
        session.Save();

        Assert.Null(session.Refresh());
        temp.Write("a.md", "One. Two changed.\n\nThree.\n");
        var changes = session.Refresh();

        Assert.NotNull(changes);
        Assert.Equal(1, changes.NeedsReview);
        Assert.Equal(1, changes.Added);
        Assert.Equal(SegmentStatus.NeedsReview, session.Document.AllSegments.ElementAt(1).Status);

        temp.Write("a.md", "One. Two changed.\n\nThree. Four.\n");
        var (_, onOpen) = FileSession.Open(store, Settings, "a.md");
        Assert.Equal(1, onOpen!.Added);
    }

    [Fact]
    public void EditingSourceWritesFileAndMarksReview()
    {
        using var temp = new TempFolder();
        var path = temp.Write("a.md", "# Title\n\nOne. Two.\n");
        var store = new ProjectStore(temp.Path);
        var (session, _) = FileSession.Open(store, Settings, "a.md");
        foreach (var s in session.Document.AllSegments)
        {
            s.Target = s.Source + "-zh";
            s.Status = SegmentStatus.Confirmed;
        }

        var stats = session.EditSourceBlock(1, "One. Two edited.");

        Assert.Equal("# Title\n\nOne. Two edited.\n", File.ReadAllText(path));
        Assert.Equal(1, stats.NeedsReview);
        Assert.Equal(("Two edited.", "Two.-zh", SegmentStatus.NeedsReview),
            session.Document.AllSegments.Select(s => (s.Source, s.Target, s.Status)).Last());
    }

    [Fact]
    public void EditingSourceIntoTwoParagraphs()
    {
        using var temp = new TempFolder();
        var path = temp.Write("a.md", "One. Two.\n");
        var store = new ProjectStore(temp.Path);
        var (session, _) = FileSession.Open(store, Settings, "a.md");

        session.EditSourceBlock(0, "One.\n\nTwo.");

        Assert.Equal("One.\n\nTwo.\n", File.ReadAllText(path));
        Assert.Equal(2, session.Document.Blocks.Count);
        Assert.Equal(2, session.Parsed.Blocks.Count);
    }

    [Fact]
    public void ExportWritesMirroredPath()
    {
        using var temp = new TempFolder();
        temp.Write("guide/a.md", "One.\n");
        var store = new ProjectStore(temp.Path);
        var (session, _) = FileSession.Open(store, Settings, "guide/a.md");
        session.Document.AllSegments.Single().Target = "一。";
        session.Document.AllSegments.Single().Status = SegmentStatus.Confirmed;

        var output = Path.Combine(temp.Path, "out");
        var result = session.ExportTo(output);

        Assert.Equal(0, result.UntranslatedCount);
        Assert.Equal("一。\n", File.ReadAllText(Path.Combine(output, "guide", "a.md")));
    }
}
