namespace CatDown.Core.Segmentation;

public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;

    public bool Contains(int offset) => Start <= offset && offset < End;

    public static TextSpan FromBounds(int start, int end) => new(start, end - start);
}
