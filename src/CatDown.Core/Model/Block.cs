namespace CatDown.Core.Model;

public sealed class Block
{
    public required string Id { get; set; }
    public BlockKind Kind { get; set; }
    public bool Verbatim { get; set; }

    // Paragraph-like blocks: the block's text without container prefixes.
    // Verbatim blocks: the raw source text of the whole block.
    public required string Source { get; set; }

    public List<Segment> Segments { get; set; } = [];
}
