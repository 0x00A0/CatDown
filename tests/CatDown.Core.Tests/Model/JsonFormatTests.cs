using CatDown.Core.Model;
using CatDown.Core.Storage;

namespace CatDown.Core.Tests.Model;

public class JsonFormatTests
{
    static FileDocument Sample() => new()
    {
        SourcePath = "docs/setup.md",
        SourceHash = "sha256:abc",
        NextId = 14,
        Blocks =
        [
            new Block
            {
                Id = "b7",
                Kind = BlockKind.Paragraph,
                Source = "Install the **CLI** with the command below. Then run it.",
                Segments =
                [
                    new Segment { Id = "s12", Source = "Install the **CLI** with the command below.", Target = "使用以下命令安装 **CLI**。", Status = SegmentStatus.Confirmed },
                    new Segment { Id = "s13", Source = "Then run it." },
                ],
            },
        ],
    };

    [Fact]
    public void FileDocumentRoundTrips()
    {
        var json = CatDownJson.Serialize(Sample());
        var back = CatDownJson.Deserialize<FileDocument>(json);

        Assert.Equal(json, CatDownJson.Serialize(back));
        Assert.Equal(SegmentStatus.Confirmed, back.Blocks[0].Segments[0].Status);
        Assert.Equal("使用以下命令安装 **CLI**。", back.Blocks[0].Segments[0].Target);
    }

    [Fact]
    public void FileDocumentUsesDocumentedFieldNames()
    {
        var json = CatDownJson.Serialize(Sample());

        Assert.Contains("\"sourcePath\": \"docs/setup.md\"", json);
        Assert.Contains("\"kind\": \"paragraph\"", json);
        Assert.Contains("\"verbatim\": false", json);
        Assert.Contains("\"status\": \"confirmed\"", json);
        Assert.Contains("\"status\": \"untranslated\"", json);
        Assert.Contains("使用以下命令安装", json);
        Assert.DoesNotContain("hasTranslation", json);
        Assert.DoesNotContain("allSegments", json);
    }

    [Fact]
    public void ProjectSettingsRoundTrips()
    {
        var settings = new ProjectSettings
        {
            SourceLanguage = "en",
            TargetLanguage = "zh",
            OutputFolder = "../out",
            Abbreviations = ["approx."],
            Layout = EditorLayout.SideBySide,
        };

        var json = CatDownJson.Serialize(settings);
        var back = CatDownJson.Deserialize<ProjectSettings>(json);

        Assert.Equal(json, CatDownJson.Serialize(back));
        Assert.Contains("\"layout\": \"sideBySide\"", json);
    }

    [Fact]
    public void IdsAreUniqueAndIncreasing()
    {
        var doc = new FileDocument { SourcePath = "a.md" };
        Assert.Equal("b1", doc.NewBlockId());
        Assert.Equal("s2", doc.NewSegmentId());
        Assert.Equal(3, doc.NextId);
    }
}
