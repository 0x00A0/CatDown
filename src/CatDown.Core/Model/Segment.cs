using System.Text.Json.Serialization;

namespace CatDown.Core.Model;

public sealed class Segment
{
    public required string Id { get; set; }
    public required string Source { get; set; }
    public string Target { get; set; } = "";
    public SegmentStatus Status { get; set; } = SegmentStatus.Untranslated;

    [JsonIgnore]
    public bool HasTranslation => Status != SegmentStatus.Untranslated;
}
