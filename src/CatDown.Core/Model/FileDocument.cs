using System.Text.Json.Serialization;

namespace CatDown.Core.Model;

public sealed class FileDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public required string SourcePath { get; set; }
    public string SourceHash { get; set; } = "";
    public int NextId { get; set; } = 1;
    public List<Block> Blocks { get; set; } = [];

    public string NewBlockId() => $"b{NextId++}";
    public string NewSegmentId() => $"s{NextId++}";

    [JsonIgnore]
    public IEnumerable<Segment> AllSegments => Blocks.SelectMany(b => b.Segments);
}
