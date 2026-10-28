using System.Collections.ObjectModel;
using Avalonia.Threading;
using CatDown.App.Localization;
using CatDown.App.Services;
using CatDown.Core.Editing;
using CatDown.Core.Glossary;
using CatDown.Core.Llm;
using CatDown.Core.Model;
using CatDown.Core.Segmentation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CatDown.App.ViewModels;

public sealed record TmMatchViewModel(int Number, string Score, string Source, string Target);

public sealed record GlossaryHitViewModel(string Source, string Target, string Note);

public partial class SegmentGridViewModel : ObservableObject
{
    readonly IDialogService _dialogs;
    readonly ProjectSettings _project;
    readonly AppServices _services;
    readonly Action<string?> _notify;
    readonly Action _saved;
    readonly Action _saveProject;
    readonly DispatcherTimer _saveTimer;
    Glossary _glossary;

    public SegmentGridViewModel(FileSession session, IDialogService dialogs, ProjectSettings project, AppServices services,
        Glossary glossary, Action<string?> notify, Action saved, Action saveProject)
    {
        Session = session;
        _dialogs = dialogs;
        _project = project;
        _services = services;
        _glossary = glossary;
        _notify = notify;
        _saved = saved;
        _saveProject = saveProject;
        _layout = project.Layout;
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

    // Every selected row (Ctrl/Shift-click in the grid). SelectedRow is the one being edited.
    public ObservableCollection<SegmentRowViewModel> SelectedRows { get; } = [];

    CancellationTokenSource? _translation;

    [ObservableProperty]
    private bool _onlyNeedsReview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewButtonText))]
    private bool _isPreview;

    // The whole translated document, rendered when the preview is on.
    [ObservableProperty]
    private string? _previewMarkdown;

    [ObservableProperty]
    private IReadOnlyList<TmMatchViewModel> _matches = [];

    [ObservableProperty]
    private IReadOnlyList<GlossaryHitViewModel> _hits = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MachineTranslateCommand), nameof(MachineTranslateAllCommand), nameof(StopTranslatingCommand))]
    private bool _isTranslating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSideBySide), nameof(LayoutButtonText))]
    private EditorLayout _layout;

    public bool IsSideBySide => Layout == EditorLayout.SideBySide;

    public string LayoutButtonText => Loc.Get(IsSideBySide ? "GridLayout" : "SideBySide");

    partial void OnLayoutChanged(EditorLayout value)
    {
        _project.Layout = value;
        _saveProject();
    }

    [RelayCommand]
    void ToggleLayout() => Layout = IsSideBySide ? EditorLayout.Grid : EditorLayout.SideBySide;

    public string PreviewButtonText => Loc.Get(IsPreview ? "BackToEditing" : "Preview");

    public bool HasPendingSave => _saveTimer.IsEnabled;

    partial void OnSelectedRowChanged(SegmentRowViewModel? oldValue, SegmentRowViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.IsEditing = false;
        if (newValue is not null)
            newValue.IsEditing = true;
        UpdateSuggestions();
    }

    partial void OnOnlyNeedsReviewChanged(bool value) => Rebuild();

    public void SetGlossary(Glossary glossary)
    {
        _glossary = glossary;
        Rebuild();
    }

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
                {
                    var hits = block.Verbatim ? [] : _glossary.FindHits(segment.Source, _project.SourceLanguage);
                    rows.Add(new SegmentRowViewModel(segment, block, number, first, hits, _ => ScheduleSave()));
                }
                first = false;
            }
        }
        SelectedRow = null;
        Rows = rows;
        SelectedRow = rows.FirstOrDefault(r => r.Segment.Id == selectedId);
    }

    void UpdateSuggestions()
    {
        if (SelectedRow is not { IsVerbatim: false } row)
        {
            Matches = [];
            Hits = [];
            return;
        }
        Matches = _services.Memory.Search(_project.SourceLanguage, _project.TargetLanguage, row.Source)
            .Select((m, i) => new TmMatchViewModel(i + 1, $"{m.Score}%", m.Source, m.Target))
            .ToList();
        Hits = row.GlossaryHits.Select(h => h.Entry).Distinct()
            .Select(e => new GlossaryHitViewModel(e.Source, e.Target, e.Note))
            .ToList();
    }

    // The rows a command acts on: all selected rows in document order, or just the edited one.
    public IReadOnlyList<SegmentRowViewModel> TargetRows() =>
        SelectedRows.Distinct().Count() > 1 ? SelectedRows.Distinct().OrderBy(Rows.IndexOf).ToList()
        : SelectedRow is { } row ? [row]
        : [];

    [RelayCommand]
    void Confirm()
    {
        var rows = TargetRows().Where(r => r.Target.Length > 0).ToList();
        if (rows.Count == 0)
            return;
        foreach (var row in rows)
        {
            row.Confirm();
            if (!row.IsVerbatim)
                _services.Memory.Add(_project.SourceLanguage, _project.TargetLanguage, row.Source, row.Target);
        }
        var index = Rows.IndexOf(rows[^1]);
        SelectedRows.Clear();
        SelectedRow = Rows.Skip(index + 1).FirstOrDefault(r => !r.IsConfirmed) ?? rows[^1];
    }

    [RelayCommand]
    void ClearTargets()
    {
        foreach (var row in TargetRows())
            row.Target = "";
    }

    [RelayCommand]
    void UseMatch(int number)
    {
        if (SelectedRow is { } row && number >= 1 && number <= Matches.Count)
            row.Target = Matches[number - 1].Target;
    }

    [RelayCommand]
    void CopySource()
    {
        if (SelectedRow is { } row)
            row.Target = row.Source;
    }

    bool CanMachineTranslate() => !IsTranslating;

    // Translates the selected rows (or the edited one). Asks before replacing existing translations.
    [RelayCommand(CanExecute = nameof(CanMachineTranslate))]
    async Task MachineTranslate()
    {
        var rows = TargetRows().Where(r => !r.IsVerbatim).ToList();
        if (rows.Count == 0)
            return;
        if (_services.LlmClient() is null)
        {
            _notify(Loc.Get("MtNotConfigured"));
            return;
        }
        if (rows.Any(r => r.Target.Length > 0) && !await _dialogs.ConfirmAsync(Loc.Get("OverwriteTitle"), Loc.Get("OverwriteMessage")))
            return;
        await TranslateRows(rows);
    }

    // Translates every sentence of the file again, replacing what is there.
    [RelayCommand(CanExecute = nameof(CanMachineTranslate))]
    async Task MachineTranslateAll()
    {
        if (_services.LlmClient() is null)
        {
            _notify(Loc.Get("MtNotConfigured"));
            return;
        }
        OnlyNeedsReview = false;
        var rows = Rows.Where(r => !r.IsVerbatim).ToList();
        if (rows.Count == 0)
            return;
        if (!await _dialogs.ConfirmAsync(Loc.Get("MachineTranslateAll"), Loc.Format("MtAllConfirm", rows.Count)))
            return;
        await TranslateRows(rows);
    }

    bool CanStopTranslating() => IsTranslating;

    [RelayCommand(CanExecute = nameof(CanStopTranslating))]
    void StopTranslating() => _translation?.Cancel();

    // One request per sentence, in order, so later sentences see the earlier translations of their paragraph.
    async Task TranslateRows(IReadOnlyList<SegmentRowViewModel> rows)
    {
        var client = _services.LlmClient()!;
        _translation = new CancellationTokenSource();
        IsTranslating = true;
        var done = 0;
        try
        {
            foreach (var row in rows)
            {
                if (rows.Count > 1)
                    _notify(Loc.Format("MtProgress", done + 1, rows.Count));
                var request = new TranslationRequest(
                    _project.SourceLanguage,
                    _project.TargetLanguage,
                    row.Source,
                    row.Block.Source,
                    row.Block.Segments.Where(s => s != row.Segment && s.HasTranslation && s.Target.Length > 0).Select(s => (s.Source, s.Target)).ToList(),
                    row.GlossaryHits.Select(h => h.Entry).Distinct().ToList());
                var reply = await client.CompleteAsync(TranslationPrompt.Build(request), _translation.Token);
                row.Target = TranslationPrompt.CleanReply(reply, row.Source);
                done++;
            }
            if (rows.Count > 1)
                _notify(Loc.Format("MtDone", done));
        }
        catch (LlmException e)
        {
            _notify(Loc.Format("MtFailed", e.Message));
        }
        catch (OperationCanceledException) when (_translation.IsCancellationRequested)
        {
            _notify(Loc.Format("MtStopped", done, rows.Count));
        }
        catch (TaskCanceledException)
        {
            _notify(Loc.Format("MtFailed", "timeout"));
        }
        finally
        {
            IsTranslating = false;
            _translation.Dispose();
            _translation = null;
            SaveNow();
        }
    }

    // Splits blocks again; asks once per translated block unless told to apply the answer to all.
    public async Task<ResegmentStats> Resegment(IEnumerable<Block> blocks, Func<Block, ParsedBlock, Task<IReadOnlyList<TextSpan>>> split)
    {
        SaveNow();
        var client = _services.LlmClient();
        TranslatedBlockAction? forAll = null;
        var resegmenter = new Resegmenter(_project.TargetLanguage, async block =>
        {
            if (forAll is { } decided)
                return decided;
            var choice = await _dialogs.AskTranslatedBlockAsync(block.Source, client is not null);
            if (choice.ApplyToAll)
                forAll = choice.Action;
            return choice.Action;
        }, client is null ? null : new LlmRealigner(client));

        var stats = await resegmenter.ResegmentAsync(Session, blocks, split);
        Rebuild();
        _saved();
        _notify(Loc.Format("ResegmentDone", stats.ChangedBlocks, stats.SkippedBlocks));
        return stats;
    }

    public Task ResegmentWithRules()
    {
        var splitter = Session.CurrentSplitter();
        return Resegment(Session.Document.Blocks,
            (block, parsed) => Task.FromResult(splitter.Split(block.Source, parsed.Protected, _project.SourceLanguage)));
    }

    [RelayCommand]
    Task LlmSegmentBlock() =>
        SelectedRow is { IsVerbatim: false } row ? LlmSegment([row.Block]) : Task.CompletedTask;

    [RelayCommand]
    Task LlmSegmentFile() => LlmSegment(Session.Document.Blocks);

    async Task LlmSegment(IEnumerable<Block> blocks)
    {
        if (_services.LlmClient() is not { } client)
        {
            _notify(Loc.Get("MtNotConfigured"));
            return;
        }
        var segmenter = new LlmSegmenter(client);
        IsTranslating = true;
        try
        {
            await Resegment(blocks, (block, parsed) => segmenter.SplitAsync(block.Source, parsed.Protected));
        }
        catch (LlmException e)
        {
            Rebuild();
            _notify(Loc.Format("LlmSegmentFailed", e.Message));
        }
        finally
        {
            IsTranslating = false;
        }
    }

    [RelayCommand]
    void TogglePreview()
    {
        PreviewMarkdown = IsPreview ? null : Session.Export().Text;
        IsPreview = !IsPreview;
    }

    [RelayCommand]
    void Merge()
    {
        var rows = TargetRows();
        if (rows.Count == 0)
            return;
        if (rows.Count == 1)
        {
            AfterEdit(SegmentOperations.Merge(Session.Document, rows[0].Segment.Id, _project.TargetLanguage));
            return;
        }

        // Several selected sentences merge into the first; they must be neighbours in one block.
        var block = rows[0].Block;
        var indexes = rows.Select(r => block.Segments.IndexOf(r.Segment)).ToList();
        if (rows.Any(r => r.Block != block) || indexes.Zip(indexes.Skip(1)).Any(p => p.Second != p.First + 1))
        {
            _notify(Loc.Get("MergeNotContiguous"));
            return;
        }
        for (var i = 1; i < rows.Count; i++)
            SegmentOperations.Merge(Session.Document, rows[0].Segment.Id, _project.TargetLanguage);
        SelectedRows.Clear();
        SelectedRow = null;
        Rebuild();
        SelectedRow = Rows.FirstOrDefault(r => r.Segment == rows[0].Segment);
        SaveNow();
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

    // Edits the whole source file at once, e.g. to write the first text of a new file.
    [RelayCommand]
    async Task EditWholeSource()
    {
        var text = await _dialogs.EditTextAsync(Loc.Get("EditWholeSource"), Session.Source.Text);
        if (text is null || text == Session.Source.Text)
            return;
        SaveNow();
        var stats = Session.ReplaceSource(text);
        Rebuild();
        SelectedRow ??= Rows.FirstOrDefault(r => !r.IsConfirmed) ?? Rows.FirstOrDefault();
        _notify(MainWindowViewModel.ChangeNotice(stats));
        _saved();
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
