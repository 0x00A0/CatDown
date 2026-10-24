using CatDown.Core.Model;

namespace CatDown.Core.Segmentation;

// One line of a block: where it sits in the source file and in the block's logical text.
public readonly record struct LinePiece(int SourceStart, int TextStart, int Length);

public sealed class ParsedBlock
{
    public required BlockKind Kind { get; init; }
    public bool Verbatim { get; init; }

    // Range of the source file this block replaces on export.
    public required TextSpan SourceSpan { get; init; }

    // Logical text: lines joined by "\n" without container prefixes.
    // For verbatim blocks this is the raw source of the block.
    public required string Text { get; init; }

    public IReadOnlyList<LinePiece> Pieces { get; init; } = [];

    // Prefix written after each newline when the block's text changes, e.g. "> " inside a quote.
    // Entry k belongs to the newline before line k + 1.
    public IReadOnlyList<string> ContinuationPrefixes { get; init; } = [];
    public string DefaultContinuationPrefix { get; init; } = "";

    // Inline markup inside Text that sentence splitting must not cut.
    public IReadOnlyList<TextSpan> Protected { get; init; } = [];
}

public sealed class ParsedFile
{
    public required string Text { get; init; }
    public required string NewLine { get; init; }
    public required IReadOnlyList<ParsedBlock> Blocks { get; init; }
}
