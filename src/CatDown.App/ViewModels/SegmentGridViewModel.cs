using System.Collections.ObjectModel;
using Avalonia.Threading;
using CatDown.App.Localization;
using CatDown.App.Services;
using CatDown.Core.Editing;
using CatDown.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CatDown.App.ViewModels;

public partial class SegmentGridViewModel : ObservableObject
{
    readonly IDialogService _dialogs;
    readonly string _targetLanguage;
    readonly Action<string?> _notify;
    readonly Action _saved;
    readonly DispatcherTimer _saveTimer;

    public SegmentGridViewModel(FileSession session, IDialogService dialogs, string targetLanguage, Action<string?> notify, Action saved)
    {
        Session = session;
        _dialogs = dialogs;
        _targetLanguage = targetLanguage;
        _notify = notify;
        _saved = saved;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _saveTimer.Tick += (_, _) => SaveNow();
        Rebuild();
        SelectedRow = Rows.FirstOrDefault(r => !r.IsConfirmed) ?? Rows.FirstOrDefault();
    }

    public FileSession Session { get; }

    [ObservableProperty]
    private ObservableCollection<SegmentRowViewModel> _rows = [];

    [ObservableProperty]
    private SegmentRowViewModel? _selectedRow;

    [ObservableProperty]
    private bool _onlyNeedsReview;

    public bool HasPendingSave => _saveTimer.IsEnabled;

    partial void OnSelectedRowChanged(SegmentRowViewModel? oldValue, SegmentRowViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.IsEditing = false;
        if (newValue is not null)
            newValue.IsEditing = true;
    }

    partial void OnOnlyNeedsReviewChanged(bool value) => Rebuild();

    // Recreates the rows from the document, keeping the selection on the same segment if it still exists.
    public void Rebuild()
    {
        var selectedId = SelectedRow?.Segment.Id;
        var rows = new ObservableCollection<SegmentRowViewModel>();
        var number = 0;
        foreach (var block in Session.Document.Blocks)
        {
            var first = true;
            foreach (var segment in block.Segments)
            {
                number++;
                if (!OnlyNeedsReview || segment.Status == SegmentStatus.NeedsReview)
                    rows.Add(new SegmentRowViewModel(segment, block, number, first, _ => ScheduleSave()));
                first = false;
            }
        }
        SelectedRow = null;
        Rows = rows;
        SelectedRow = rows.FirstOrDefault(r => r.Segment.Id == selectedId);
    }

    [RelayCommand]
    void Confirm()
    {
        if (SelectedRow is not { } row)
            return;
        row.Confirm();
        var index = Rows.IndexOf(row);
        SelectedRow = Rows.Skip(index + 1).FirstOrDefault(r => !r.IsConfirmed) ?? SelectedRow;
    }

    [RelayCommand]
    void CopySource()
    {
        if (SelectedRow is { } row)
            row.Target = row.Source;
    }

    [RelayCommand]
    void Merge()
    {
        if (SelectedRow is not { } row)
            return;
        var result = SegmentOperations.Merge(Session.Document, row.Segment.Id, _targetLanguage);
        AfterEdit(result);
    }

    [RelayCommand]
    void Split()
    {
        if (SelectedRow is not { } row)
            return;
        var parsed = Session.ParsedBlockOf(row.Block);
        var result = SegmentOperations.Split(Session.Document, parsed, row.Segment.Id, row.SourceCaret);
        AfterEdit(result);
    }

    void AfterEdit(SegmentEditResult result)
    {
        var message = result switch
        {
            SegmentEditResult.Done => null,
            SegmentEditResult.NoNextSegment => "CannotMergeLast",
            SegmentEditResult.EmptySide => "CannotSplitEdge",
            SegmentEditResult.InsideMarkup => "CannotSplitMarkup",
            SegmentEditResult.Verbatim => "CannotSplitVerbatim",
            _ => null,
        };
        if (message is not null)
        {
            _notify(Loc.Get(message));
            return;
        }
        Rebuild();
        SaveNow();
    }

    [RelayCommand]
    async Task EditSource()
    {
        if (SelectedRow is not { } row)
            return;
        var text = await _dialogs.EditTextAsync(Loc.Get("EditSource"), row.Block.Source);
        if (text is not null && text != row.Block.Source)
            EditSource(row.Block, text);
    }

    // Replaces a block's source text and writes it to the source file.
    public void EditSource(Block block, string newText)
    {
        SaveNow();
        var index = Session.Document.Blocks.IndexOf(block);
        var stats = Session.EditSourceBlock(index, newText);
        Rebuild();
        _notify(MainWindowViewModel.ChangeNotice(stats));
        _saved();
    }

    // Rereads the source after an outside change; returns the notice to show, if any.
    public string? Refresh()
    {
        SaveNow();
        var changes = Session.Refresh();
        if (changes is null)
            return null;
        Rebuild();
        _saved();
        return MainWindowViewModel.ChangeNotice(changes);
    }

    public void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow()
    {
        _saveTimer.Stop();
        Session.Save();
        _saved();
    }
}
