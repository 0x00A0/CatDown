namespace CatDown.Core.Alignment;

public enum PairKind
{
    Same,
    Changed,
    Added,
    Removed,
}

// Old or New is -1 when that side has no item.
public readonly record struct Pair(PairKind Kind, int Old, int New);

// Lines up two versions of a list of texts:
// 1. longest common subsequence of identical texts,
// 2. identical texts that moved elsewhere,
// 3. what is left between two matches is paired by position as "changed".
public static class SequenceAligner
{
    public static List<Pair> Align(IReadOnlyList<string> oldItems, IReadOnlyList<string> newItems)
    {
        var oldMatch = Enumerable.Repeat(-1, oldItems.Count).ToArray();
        var newMatch = Enumerable.Repeat(-1, newItems.Count).ToArray();
        var anchors = CommonSubsequence(oldItems, newItems);
        foreach (var (o, n) in anchors)
        {
            oldMatch[o] = n;
            newMatch[n] = o;
        }

        var moved = new Dictionary<string, Queue<int>>();
        for (var o = 0; o < oldItems.Count; o++)
        {
            if (oldMatch[o] < 0)
            {
                if (!moved.TryGetValue(oldItems[o], out var queue))
                    moved[oldItems[o]] = queue = new Queue<int>();
                queue.Enqueue(o);
            }
        }
        for (var n = 0; n < newItems.Count; n++)
        {
            if (newMatch[n] < 0 && moved.TryGetValue(newItems[n], out var queue) && queue.Count > 0)
            {
                var o = queue.Dequeue();
                oldMatch[o] = n;
                newMatch[n] = o;
            }
        }

        var changed = new Dictionary<int, int>();
        var removed = new List<int>();
        var bounds = new List<(int Old, int New)> { (-1, -1) };
        bounds.AddRange(anchors);
        bounds.Add((oldItems.Count, newItems.Count));
        for (var k = 0; k + 1 < bounds.Count; k++)
        {
            var olds = Range(bounds[k].Old + 1, bounds[k + 1].Old).Where(o => oldMatch[o] < 0).ToList();
            var news = Range(bounds[k].New + 1, bounds[k + 1].New).Where(n => newMatch[n] < 0).ToList();
            var paired = Math.Min(olds.Count, news.Count);
            for (var i = 0; i < paired; i++)
                changed[news[i]] = olds[i];
            removed.AddRange(olds.Skip(paired));
        }

        var pairs = new List<Pair>(newItems.Count + removed.Count);
        for (var n = 0; n < newItems.Count; n++)
        {
            if (newMatch[n] >= 0)
                pairs.Add(new Pair(PairKind.Same, newMatch[n], n));
            else if (changed.TryGetValue(n, out var o))
                pairs.Add(new Pair(PairKind.Changed, o, n));
            else
                pairs.Add(new Pair(PairKind.Added, -1, n));
        }
        pairs.AddRange(removed.Select(o => new Pair(PairKind.Removed, o, -1)));
        return pairs;
    }

    static IEnumerable<int> Range(int from, int to) => from < to ? Enumerable.Range(from, to - from) : [];

    static List<(int Old, int New)> CommonSubsequence(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        // Strip the common prefix and suffix first; most edits touch a small part of a file.
        var prefix = 0;
        while (prefix < a.Count && prefix < b.Count && a[prefix] == b[prefix])
            prefix++;
        var suffix = 0;
        while (suffix < a.Count - prefix && suffix < b.Count - prefix && a[a.Count - 1 - suffix] == b[b.Count - 1 - suffix])
            suffix++;

        var result = new List<(int, int)>();
        for (var i = 0; i < prefix; i++)
            result.Add((i, i));

        var n = a.Count - prefix - suffix;
        var m = b.Count - prefix - suffix;
        var table = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                table[i, j] = a[prefix + i] == b[prefix + j] ? table[i + 1, j + 1] + 1 : Math.Max(table[i + 1, j], table[i, j + 1]);

        for (int i = 0, j = 0; i < n && j < m;)
        {
            if (a[prefix + i] == b[prefix + j])
            {
                result.Add((prefix + i, prefix + j));
                i++;
                j++;
            }
            else if (table[i + 1, j] >= table[i, j + 1])
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        for (var k = suffix; k > 0; k--)
            result.Add((a.Count - k, b.Count - k));
        return result;
    }
}
