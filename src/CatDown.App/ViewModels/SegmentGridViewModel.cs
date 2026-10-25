using System.Collections.ObjectModel;
using Avalonia.Threading;
using CatDown.App.Localization;
using CatDown.App.Services;
using CatDown.Core.Editing;
using CatDown.Core.Glossary;
using CatDown.Core.Llm;
using CatDown.Core.Model;
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
    [NotifyCanExecuteChangedFor(nameof(MachineTranslateCommand))]
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

    [RelayCommand]
    void Confirm()
    {
        if (SelectedRow is not { } row || row.Target.Length == 0)
            return;
        row.Confirm();
        if (!row.IsVerbatim)
            _services.Memory.Add(_project.SourceLanguage, _project.TargetLanguage, row.Source, row.Target);
        var index = Rows.IndexOf(row);
        SelectedRow = Rows.Skip(index + 1).FirstOrDefault(r => !r.IsConfirmed) ?? SelectedRow;
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

    [RelayCommand(CanExecute = nameof(CanMachineTranslate))]
    async Task MachineTranslate()
    {
        if (SelectedRow is not { IsVerbatim: false } row)
            return;
        var client = _services.LlmClient();
        if (client is null)
        {
            _notify(Loc.Get("MtNotConfigured"));
            return;
        }
        if (row.Target.Length > 0 && !await _dialogs.ConfirmAsync(Loc.Get("OverwriteTitle"), Loc.Get("OverwriteMessage")))
            return;

        var request = new TranslationRequest(
            _project.SourceLanguage,
            _project.TargetLanguage,
            row.Source,
            row.Block.Source,
            row.Block.Segments.Where(s => s != row.Segment && s.HasTranslation && s.Target.Length > 0).Select(s => (s.Source, s.Target)).ToList(),
            row.GlossaryHits.Select(h => h.Entry).Distinct().ToList());

        IsTranslating = true;
        try
        {
            var reply = await client.CompleteAsync(TranslationPrompt.Build(request));
            row.Target = TranslationPrompt.CleanReply(reply, row.Source);
        }
        catch (LlmException e)
        {
            _notify(Loc.Format("MtFailed", e.Message));
        }
        catch (TaskCanceledException)
        {
            _notify(Loc.Format("MtFailed", "timeout"));
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
        if (SelectedRow is not { } row)
            return;
        var result = SegmentOperations.Merge(Session.Document, row.Segment.Id, _project.TargetLanguage);
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
