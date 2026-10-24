using System.Text;
using CatDown.Core.Model;
using CatDown.Core.Segmentation;
using CatDown.Core.Storage;

namespace CatDown.Core.Tests.Storage;

public class ProjectStoreTests
{
    [Fact]
    public void NewProjectHasNoSettings()
    {
        using var temp = new TempFolder();
        Assert.Null(new ProjectStore(temp.Path).LoadSettings());
    }

    [Fact]
    public void SettingsRoundTrip()
    {
        using var temp = new TempFolder();
        var store = new ProjectStore(temp.Path);
        store.SaveSettings(new ProjectSettings { SourceLanguage = "en", TargetLanguage = "zh", OutputFolder = "out" });

        var settings = store.LoadSettings()!;
        Assert.Equal("zh", settings.TargetLanguage);
        Assert.True(File.Exists(Path.Combine(temp.Path, ".catdown", "project.json")));
    }

    [Fact]
    public void ListsMarkdownFilesAndSkipsDataGitModulesAndOutput()
    {
        using var temp = new TempFolder();
        temp.Write("README.md", "# a");
        temp.Write("guide/setup.md", "b");
        temp.Write("guide/Notes.markdown", "c");
        temp.Write("guide/image.png", "x");
        temp.Write(".catdown/files/x.md", "x");
        temp.Write(".git/y.md", "y");
        temp.Write("node_modules/pkg/readme.md", "z");
        temp.Write("out/README.md", "translated");

        var store = new ProjectStore(temp.Path);
        var settings = new ProjectSettings { SourceLanguage = "en", TargetLanguage = "zh", OutputFolder = "out" };

        Assert.Equal(["guide/Notes.markdown", "guide/setup.md", "README.md"], store.ListMarkdownFiles(settings));
        Assert.Contains("out/README.md", store.ListMarkdownFiles(null));
    }

    [Fact]
    public void FileDocumentRoundTrip()
    {
        using var temp = new TempFolder();
        var store = new ProjectStore(temp.Path);
        var doc = new DocumentBuilder(new RuleSentenceSplitter(), "en").Build("guide/setup.md", BlockParser.Parse("One. Two."));

        Assert.Equal(FileLoadStatus.Missing, store.LoadFile("guide/setup.md").Status);
        store.SaveFile(doc);
        var loaded = store.LoadFile("guide/setup.md");

        Assert.Equal(FileLoadStatus.Loaded, loaded.Status);
        Assert.Equal(CatDownJson.Serialize(doc), CatDownJson.Serialize(loaded.Document));
        Assert.True(File.Exists(Path.Combine(temp.Path, ".catdown", "files", "guide", "setup.md.json")));
    }

    [Fact]
    public void BrokenJsonIsBackedUp()
    {
        using var temp = new TempFolder();
        var path = temp.Write(".catdown/files/a.md.json", "{ not json");
        var store = new ProjectStore(temp.Path);

        var result = store.LoadFile("a.md");

        Assert.Equal(FileLoadStatus.Broken, result.Status);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(result.BackupPath));
        Assert.Matches(@"a\.md\.broken-\d{14}\.json$", result.BackupPath);
        Assert.Equal("{ not json", File.ReadAllText(result.BackupPath!));
    }

    [Fact]
    public void FailedWriteLeavesOriginalUntouched()
    {
        using var temp = new TempFolder();
        var path = temp.Write("a.txt", "original");

        Assert.Throws<IOException>(() => ProjectStore.AtomicWrite(path, stream =>
        {
            stream.Write("partial"u8);
            throw new IOException("disk full");
        }));

        Assert.Equal("original", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(temp.Path));
    }

    [Fact]
    public void SourceKeepsByteOrderMark()
    {
        using var temp = new TempFolder();
        var path = Path.Combine(temp.Path, "bom.md");
        File.WriteAllBytes(path, [.. Encoding.UTF8.Preamble, .. "# 标题"u8.ToArray()]);
        var store = new ProjectStore(temp.Path);

        var source = store.ReadSource("bom.md");
        Assert.True(source.HasBom);
        Assert.Equal("# 标题", source.Text);

        store.WriteSource("bom.md", source with { Text = "# 新标题" });
        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.Equal("# 新标题", new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3));
    }
}
