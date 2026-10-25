using System.Collections.ObjectModel;
using CatDown.App.Localization;
using CatDown.App.Services;
using CatDown.Core.Alignment;
using CatDown.Core.Editing;
using CatDown.Core.Glossary;
using CatDown.Core.Model;
using CatDown.Core.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CatDown.App.ViewModels;

public partial class MainWindowViewModel(IDialogService dialogs, AppServices services) : ObservableObject
{
    public IDialogService Dialogs { get; } = dialogs;
    public AppServices Services { get; } = services;

    Glossary _glossary = new();

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

    [RelayCommand]
    async Task OpenGlossary()
    {
        if (Store is null)
            return;
        if (await Dialogs.EditGlossaryAsync(_glossary) is not { } edited)
            return;
        Store.SaveGlossary(edited);
        _glossary = edited;
        Editor?.SetGlossary(edited);
    }

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
        _glossary = store.LoadGlossary();
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
        Editor = new SegmentGridViewModel(session, Dialogs, Settings, Services, _glossary, notice => Notice = notice, UpdateProgress,
            () => Store.SaveSettings(Settings));
        Notice = session.BrokenBackupPath is { } backup ? Loc.Format("BrokenFile", backup) : ChangeNotice(changes);
        UpdateProgress();
    }

    // The window came back to the front: the source may have been changed elsewhere.
    public void OnActivated()
    {
        if (Store is null || Editor is null || Session is null)
            return;

        var path = Session.RelativePath;
        if (!File.Exists(Store.ToFull(path)))
        {
            CloseFile();
            ReloadFiles();
            Notice = Loc.Format("SourceMissing", path);
            return;
        }

        try
        {
            if (Editor.Refresh() is { } notice)
                Notice = notice;
        }
        catch (IOException)
        {
            // Another program may be writing the file right now; check again next time.
        }
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

    [RelayCommand]
    async Task ExportCurrent()
    {
        if (Session is null || await EnsureOutputFolder() is not { } output)
            return;
        Editor?.SaveNow();
        await Export([Session.RelativePath], output);
    }

    [RelayCommand]
    async Task ExportAll()
    {
        if (Store is null || await EnsureOutputFolder() is not { } output)
            return;
        Editor?.SaveNow();
        await Export(Store.ListMarkdownFiles(Settings), output);
    }

    // The output folder from the project settings; asks for one the first time.
    async Task<string?> EnsureOutputFolder()
    {
        if (Store is null || Settings is null)
            return null;
        if (Store.ResolveOutputFolder(Settings) is { } existing)
            return existing;

        var picked = await Dialogs.PickFolderAsync(Loc.Get("ChooseOutputFolder"));
        if (picked is null)
            return null;
        if (string.Equals(Path.GetFullPath(picked).TrimEnd(Path.DirectorySeparatorChar), Store.Root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            await Dialogs.ShowMessageAsync(Loc.Get("ExportTitle"), Loc.Get("OutputIsProjectRoot"));
            return null;
        }

        Settings.OutputFolder = Path.GetRelativePath(Store.Root, picked).Replace('\\', '/');
        Store.SaveSettings(Settings);
        ReloadFiles();
        return Store.ResolveOutputFolder(Settings);
    }

    async Task Export(IReadOnlyList<string> paths, string output)
    {
        var exported = 0;
        var untranslated = 0;
        var failures = new List<string>();
        foreach (var path in paths)
        {
            try
            {
                var session = Session?.RelativePath == path ? Session : FileSession.Open(Store!, Settings!, path).Session;
                untranslated += session.ExportTo(output).UntranslatedCount;
                exported++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                failures.Add($"{path}: {e.Message}");
            }
        }

        var message = Loc.Format("ExportDone", exported, untranslated);
        if (failures.Count > 0)
            message += "\n\n" + Loc.Format("ExportFailed", failures.Count) + "\n" + string.Join("\n", failures);
        var selected = Session?.RelativePath;
        ReloadFiles();
        if (selected is not null)
            UpdateProgress();
        await Dialogs.ShowMessageAsync(Loc.Get("ExportTitle"), message);
    }

    public static string? ChangeNotice(AlignmentStats? changes) =>
        changes is { HasChanges: true } c ? Loc.Format("SourceChanged", c.NeedsReview, c.Added, c.Removed) : null;
}
