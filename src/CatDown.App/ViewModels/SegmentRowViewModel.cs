using CatDown.App.Controls;
using CatDown.App.Localization;
using CatDown.Core.Glossary;
using CatDown.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CatDown.App.ViewModels;

public partial class SegmentRowViewModel : ObservableObject
{
    readonly Action<SegmentRowViewModel> _changed;

    public SegmentRowViewModel(Segment segment, Block block, int number, bool isBlockStart,
        IReadOnlyList<GlossaryHit> glossaryHits, Action<SegmentRowViewModel> changed)
    {
        Segment = segment;
        Block = block;
        Number = number;
        IsBlockStart = isBlockStart;
        GlossaryHits = glossaryHits;
        _changed = changed;
        SourceParts = Parts(segment.Source, glossaryHits);
        GlossaryTip = glossaryHits.Count == 0 ? null : string.Join("\n", glossaryHits
            .Select(h => h.Entry).Distinct()
            .Select(e => e.Note.Length > 0 ? $"{e.Source} → {e.Target} · {e.Note}" : $"{e.Source} → {e.Target}"));
    }

    public Segment Segment { get; }
    public Block Block { get; }
    public int Number { get; }
    public bool IsBlockStart { get; }
    public bool IsVerbatim => Block.Verbatim;
    public string Source => Segment.Source;
    public IReadOnlyList<GlossaryHit> GlossaryHits { get; }
    public IReadOnlyList<TextPart> SourceParts { get; }
    public string? GlossaryTip { get; }

    // Caret position in the source column; the split point.
    [ObservableProperty]
    private int _sourceCaret;

    // Set on the selected row; the view shows the editor for it.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditTarget))]
    private bool _isEditing;

    public SegmentRowViewModel? EditTarget => IsEditing ? this : null;

    public string Target
    {
        get => Segment.Target;
        set
        {
            if (value == Segment.Target)
                return;
            Segment.Target = value;
            Segment.Status = value.Length == 0 ? SegmentStatus.Untranslated : SegmentStatus.Draft;
            OnPropertyChanged();
            StatusChanged();
            _changed(this);
        }
    }

    public SegmentStatus Status => Segment.Status;
    public string StatusText => Loc.Get($"Status_{Segment.Status}");
    public bool IsUntranslated => Segment.Status == SegmentStatus.Untranslated;
    public bool IsDraft => Segment.Status == SegmentStatus.Draft;
    public bool IsConfirmed => Segment.Status == SegmentStatus.Confirmed;
    public bool NeedsReview => Segment.Status == SegmentStatus.NeedsReview;

    public string StatusGlyph => Segment.Status switch
    {
        SegmentStatus.Confirmed => "✓",
        SegmentStatus.Draft => "✎",
        SegmentStatus.NeedsReview => "!",
        _ => "○",
    };

    public void Confirm()
    {
        if (Segment.Target.Length == 0)
            return;
        Segment.Status = SegmentStatus.Confirmed;
        StatusChanged();
        _changed(this);
    }

    void StatusChanged()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusGlyph));
        OnPropertyChanged(nameof(IsUntranslated));
        OnPropertyChanged(nameof(IsDraft));
        OnPropertyChanged(nameof(IsConfirmed));
        OnPropertyChanged(nameof(NeedsReview));
    }

    static List<TextPart> Parts(string text, IReadOnlyList<GlossaryHit> hits)
    {
        var parts = new List<TextPart>();
        var cursor = 0;
        foreach (var hit in hits)
        {
            if (hit.Span.Start > cursor)
                parts.Add(new TextPart(text[cursor..hit.Span.Start], false));
            parts.Add(new TextPart(text.Substring(hit.Span.Start, hit.Span.Length), true));
            cursor = hit.Span.End;
        }
        if (cursor < text.Length)
            parts.Add(new TextPart(text[cursor..], false));
        return parts;
    }
}
