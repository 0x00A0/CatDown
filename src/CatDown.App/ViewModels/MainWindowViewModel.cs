using System.Collections.ObjectModel;
using CatDown.App.Localization;
using CatDown.App.Services;
using CatDown.Core.Alignment;
using CatDown.Core.Editing;
using CatDown.Core.Model;
using CatDown.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CatDown.App.ViewModels;

public partial class MainWindowViewModel(IDialogService dialogs) : ObservableObject
{
    public IDialogService Dialogs { get; } = dialogs;

    [ObservableProperty]
    private string _title = "CatDown";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    private ProjectStore? _store;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LanguagePairText))]
    private ProjectSettings? _settings;

    [ObservableProperty]
    private ObservableCollection<FileNode> _files = [];

    [ObservableProperty]
    private FileNode? _selectedNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    private FileSession? _session;

    [ObservableProperty]
    private SegmentGridViewModel? _editor;

    // A one-line message shown above the editor, e.g. after the source changed.
    [ObservableProperty]
    private string? _notice;

    public bool HasProject => Store is not null;
    public bool HasFile => Session is not null;

    public string LanguagePairText =>
        Settings is null ? "" : $"{Settings.SourceLanguage.ToUpperInvariant()} → {Settings.TargetLanguage.ToUpperInvariant()}";

    [RelayCommand]
    async Task OpenFolder()
    {
        var folder = await Dialogs.PickFolderAsync(Loc.Get("OpenFolder"));
        if (folder is not null)
            await OpenProject(folder);
    }

    [RelayCommand]
    Task OpenSettings() => Dialogs.ShowSettingsAsync();

    [RelayCommand]
    void DismissNotice() => Notice = null;

    public async Task<bool> OpenProject(string folder)
    {
        var store = new ProjectStore(folder);
        var settings = store.LoadSettings();
        if (settings is null)
        {
            var languages = await Dialogs.AskLanguagesAsync();
            if (languages is null)
                return false;
            settings = new ProjectSettings { SourceLanguage = languages.Source, TargetLanguage = languages.Target };
            store.SaveSettings(settings);
        }

        CloseFile();
        Store = store;
        Settings = settings;
        Title = $"{Path.GetFileName(store.Root.TrimEnd(Path.DirectorySeparatorChar))} - CatDown";
        ReloadFiles();
        Notice = Files.Count == 0 ? Loc.Get("NoMarkdownFiles") : null;
        return true;
    }

    public void ReloadFiles()
    {
        if (Store is null)
            return;
        Files = FileNode.BuildTree(Store.ListMarkdownFiles(Settings), Store.ReadProgress);
    }

    partial void OnSelectedNodeChanged(FileNode? value)
    {
        if (value?.RelativePath is { } path)
            OpenFile(path);
    }

    public void OpenFile(string relativePath)
    {
        if (Store is null || Settings is null)
            return;
        CloseFile();
        var (session, changes) = FileSession.Open(Store, Settings, relativePath);
        Session = session;
        Editor = new SegmentGridViewModel(session, Settings.TargetLanguage, notice => Notice = notice, UpdateProgress);
        Notice = session.BrokenBackupPath is { } backup ? Loc.Format("BrokenFile", backup) : ChangeNotice(changes);
        UpdateProgress();
    }

    public void CloseFile()
    {
        Editor?.SaveNow();
        Editor = null;
        Session = null;
    }

    public void UpdateProgress()
    {
        if (Session is null)
            return;
        var node = Files.SelectMany(f => f.Flatten().Prepend(f)).FirstOrDefault(n => n.RelativePath == Session.RelativePath);
        if (node is not null)
            node.Progress = ProjectStore.Progress(Session.Document);
    }

    public static string? ChangeNotice(AlignmentStats? changes) =>
        changes is { HasChanges: true } c ? Loc.Format("SourceChanged", c.NeedsReview, c.Added, c.Removed) : null;
}
