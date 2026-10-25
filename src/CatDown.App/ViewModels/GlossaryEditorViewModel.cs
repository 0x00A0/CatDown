using System.Collections.ObjectModel;
using CatDown.App.Localization;
using CatDown.App.Services;
using CatDown.Core.Glossary;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CatDown.App.ViewModels;

public partial class GlossaryEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _source = "";

    [ObservableProperty]
    private string _target = "";

    [ObservableProperty]
    private string _note = "";
}

public partial class GlossaryEditorViewModel : ObservableObject
{
    readonly IDialogService? _dialogs;

    public GlossaryEditorViewModel(Glossary glossary, IDialogService? dialogs = null)
    {
        _dialogs = dialogs;
        foreach (var entry in glossary.Entries)
            Entries.Add(new GlossaryEntryViewModel { Source = entry.Source, Target = entry.Target, Note = entry.Note });
    }

    public ObservableCollection<GlossaryEntryViewModel> Entries { get; } = [];

    [ObservableProperty]
    private string? _status;

    [RelayCommand]
    void Add() => Entries.Add(new GlossaryEntryViewModel());

    [RelayCommand]
    async Task ImportCsv()
    {
        if (_dialogs is null || await _dialogs.PickFileAsync(Loc.Get("ImportCsv"), "csv") is not { } path)
            return;
        try
        {
            var current = ToGlossary();
            var added = GlossaryCsv.Merge(current, GlossaryCsv.Read(await File.ReadAllTextAsync(path)));
            Entries.Clear();
            foreach (var entry in current.Entries)
                Entries.Add(new GlossaryEntryViewModel { Source = entry.Source, Target = entry.Target, Note = entry.Note });
            Status = Loc.Format("CsvImported", added);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = Loc.Format("FileInvalid", e.Message);
        }
    }

    [RelayCommand]
    async Task ExportCsv()
    {
        if (_dialogs is null || await _dialogs.PickSavePathAsync(Loc.Get("ExportCsv"), "glossary.csv", "csv") is not { } path)
            return;
        try
        {
            await File.WriteAllTextAsync(path, GlossaryCsv.Write(ToGlossary()), new System.Text.UTF8Encoding(true));
            Status = Loc.Format("CsvExported", path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = Loc.Format("FileInvalid", e.Message);
        }
    }

    [RelayCommand]
    void Remove(GlossaryEntryViewModel entry) => Entries.Remove(entry);

    // Rows without a term are dropped.
    public Glossary ToGlossary() => new()
    {
        Entries = Entries
            .Where(e => e.Source.Trim().Length > 0)
            .Select(e => new GlossaryEntry { Source = e.Source.Trim(), Target = e.Target.Trim(), Note = e.Note.Trim() })
            .ToList(),
    };
}
