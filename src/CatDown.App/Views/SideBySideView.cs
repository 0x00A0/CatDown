using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CatDown.App.Editor;
using CatDown.App.ViewModels;

namespace CatDown.App.Views;

// Layout B: source and translation as two documents, one segment per line, blocks separated by a
// blank line. Separators in the translation are locked, so each segment is the text between two of them.
public class SideBySideView : UserControl
{
    readonly TextEditor _source = new()
    {
        IsReadOnly = true,
        WordWrap = true,
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        Padding = new Thickness(12, 4, 4, 4),
    };
    readonly TextEditor _target = new()
    {
        WordWrap = true,
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        Padding = new Thickness(4),
    };
    readonly SegmentHighlighter _sourceHighlight;
    readonly SegmentHighlighter _targetHighlight;
    readonly StatusMargin _status = new();

    SegmentGridViewModel? _vm;
    IReadOnlyList<SegmentRowViewModel> _rows = [];
    List<(int Start, int End)> _sourceRanges = [];
    TextSegmentCollection<TextSegment> _separators;
    bool _updating;

    public SideBySideView()
    {
        EditorTheme.Apply(_source);
        EditorTheme.Apply(_target);
        _sourceHighlight = new SegmentHighlighter(this);
        _targetHighlight = new SegmentHighlighter(this);
        _source.TextArea.TextView.BackgroundRenderers.Add(_sourceHighlight);
        _target.TextArea.TextView.BackgroundRenderers.Add(_targetHighlight);
        _target.TextArea.LeftMargins.Insert(0, _status);
        _separators = new TextSegmentCollection<TextSegment>(_target.Document);

        _target.Document.Changed += (_, e) => OnTargetEdited(e.Offset);
        _target.TextArea.Caret.PositionChanged += (_, _) => OnTargetCaretMoved();
        _source.TextArea.Caret.PositionChanged += (_, _) => OnSourceCaretMoved();
        _target.TextArea.TextView.ScrollOffsetChanged += (_, _) => SyncScroll();

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,4,*") };
        Grid.SetColumn(_source, 0);
        var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Columns };
        splitter.Bind(BackgroundProperty, this.GetResourceObservable("SemiColorBorder"));
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(_target, 2);
        grid.Children.Add(_source);
        grid.Children.Add(splitter);
        grid.Children.Add(_target);
        Content = grid;
    }

    public TextEditor SourceEditor => _source;
    public TextEditor TargetEditor => _target;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelChanged;
        _vm = DataContext as SegmentGridViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnViewModelChanged;
        Rebuild();
    }

    void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SegmentGridViewModel.Rows):
            case nameof(SegmentGridViewModel.IsSideBySide):
                Rebuild();
                break;
            case nameof(SegmentGridViewModel.SelectedRow):
                if (!_updating)
                    ShowSelected(moveCaret: true);
                break;
        }
    }

    // Region k of the translation: between separator k - 1 and separator k.
    (int Start, int End) TargetRange(int k)
    {
        var start = k == 0 ? 0 : _separators.ElementAt(k - 1).EndOffset;
        var end = k == _rows.Count - 1 ? _target.Document.TextLength : _separators.ElementAt(k).StartOffset;
        return (start, end);
    }

    int TargetIndexAt(int offset)
    {
        var k = 0;
        foreach (var separator in _separators)
        {
            if (offset < separator.EndOffset && offset <= separator.StartOffset)
                return k;
            k++;
        }
        return Math.Min(k, _rows.Count - 1);
    }

    public void Rebuild()
    {
        foreach (var row in _rows)
            row.PropertyChanged -= OnRowChanged;
        _rows = _vm is { IsSideBySide: true } ? _vm.Rows.ToList() : [];
        foreach (var row in _rows)
            row.PropertyChanged += OnRowChanged;

        _updating = true;
        var source = new StringBuilder();
        var target = new StringBuilder();
        var separators = new List<(int Start, int Length)>();
        _sourceRanges = [];
        for (var k = 0; k < _rows.Count; k++)
        {
            if (k > 0)
            {
                var separator = _rows[k].IsBlockStart ? "\n\n" : "\n";
                source.Append(separator);
                separators.Add((target.Length, separator.Length));
                target.Append(separator);
            }
            _sourceRanges.Add((source.Length, source.Length + _rows[k].Source.Length));
            source.Append(_rows[k].Source);
            target.Append(_rows[k].Target);
        }

        _source.Document.Text = source.ToString();
        _target.Document.Text = target.ToString();
        _separators = new TextSegmentCollection<TextSegment>(_target.Document);
        foreach (var (start, length) in separators)
            _separators.Add(new TextSegment { StartOffset = start, Length = length });
        // Setting IsReadOnly replaces the read-only section provider, so it has to come first.
        _target.IsReadOnly = _rows.Count == 0;
        _target.TextArea.ReadOnlySectionProvider = new AtomicReadOnlySectionProvider(_separators);
        UpdateStatusMargin();
        _updating = false;
        ShowSelected(moveCaret: true);
    }

    void UpdateStatusMargin()
    {
        var starts = new List<int> { 0 };
        starts.AddRange(_separators.Select(s => s.EndOffset));
        _status.Segments = _rows.Select((row, k) => (starts[k], row)).ToList();
        _status.InvalidateVisual();
    }

    // The user typed in the translation: copy the changed region back to its segment.
    // Edits never cross a separator, so one change touches one region.
    void OnTargetEdited(int offset)
    {
        if (_updating || _rows.Count == 0)
            return;
        var k = TargetIndexAt(offset);
        var (start, end) = TargetRange(k);
        var text = _target.Document.GetText(start, end - start);
        if (_rows[k].Target == text)
            return;
        _updating = true;
        _rows[k].Target = text;
        _updating = false;
        UpdateStatusMargin();
    }

    // A segment changed from elsewhere (machine translation, a TM match, copy source): replace its region.
    void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SegmentRowViewModel row)
            return;
        if (e.PropertyName == nameof(SegmentRowViewModel.Status))
        {
            _status.InvalidateVisual();
            return;
        }
        if (_updating || e.PropertyName != nameof(SegmentRowViewModel.Target))
            return;
        var k = IndexOf(row);
        if (k < 0)
            return;
        var (start, end) = TargetRange(k);
        _updating = true;
        _target.Document.Replace(start, end - start, row.Target);
        _updating = false;
        UpdateStatusMargin();
    }

    int IndexOf(SegmentRowViewModel row)
    {
        for (var k = 0; k < _rows.Count; k++)
        {
            if (ReferenceEquals(_rows[k], row))
                return k;
        }
        return -1;
    }

    void OnTargetCaretMoved()
    {
        if (_updating || _vm is null || _rows.Count == 0)
            return;
        var k = TargetIndexAt(_target.CaretOffset);
        Select(k);
    }

    void OnSourceCaretMoved()
    {
        if (_updating || _vm is null || _rows.Count == 0)
            return;
        var offset = _source.CaretOffset;
        var k = _sourceRanges.FindIndex(r => offset <= r.End);
        if (k < 0)
            k = _rows.Count - 1;
        Select(k);
        _rows[k].SourceCaret = Math.Clamp(offset - _sourceRanges[k].Start, 0, _rows[k].Source.Length);
    }

    void Select(int k)
    {
        if (ReferenceEquals(_vm!.SelectedRow, _rows[k]))
        {
            ShowSelected(moveCaret: false);
            return;
        }
        _updating = true;
        _vm.SelectedRow = _rows[k];
        _updating = false;
        ShowSelected(moveCaret: false);
    }

    void ShowSelected(bool moveCaret)
    {
        var k = _vm?.SelectedRow is { } selected ? IndexOf(selected) : -1;
        if (k < 0)
        {
            _sourceHighlight.Range = null;
            _targetHighlight.Range = null;
        }
        else
        {
            _sourceHighlight.Range = _sourceRanges[k];
            var range = TargetRange(k);
            _targetHighlight.Range = range;
            if (moveCaret)
            {
                _updating = true;
                _target.CaretOffset = range.End;
                _updating = false;
                _target.TextArea.Caret.BringCaretToView();
            }
            ScrollSourceTo(k);
        }
        _source.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
        _target.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
    }

    void SyncScroll()
    {
        if (_rows.Count == 0)
            return;
        var view = _target.TextArea.TextView;
        if (view.GetDocumentLineByVisualTop(view.ScrollOffset.Y) is { } line)
            ScrollSourceTo(TargetIndexAt(line.Offset), align: true);
    }

    void ScrollSourceTo(int k, bool align = false)
    {
        var line = _source.Document.GetLineByOffset(_sourceRanges[k].Start);
        if (align)
            _source.ScrollToVerticalOffset(_source.TextArea.TextView.GetVisualTopByDocumentLine(line.LineNumber));
        else
            _source.ScrollToLine(line.LineNumber);
    }
}
