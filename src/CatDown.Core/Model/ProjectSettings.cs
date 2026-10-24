namespace CatDown.Core.Model;

public enum EditorLayout
{
    Grid,
    SideBySide,
}

public sealed class ProjectSettings
{
    public int Version { get; set; } = 1;
    public required string SourceLanguage { get; set; }
    public required string TargetLanguage { get; set; }

    // Relative to the project folder, or absolute. Null until the first export.
    public string? OutputFolder { get; set; }

    public List<string> Abbreviations { get; set; } = [];
    public EditorLayout Layout { get; set; } = EditorLayout.Grid;
}
