using System.Collections.ObjectModel;
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
    public GlossaryEditorViewModel(Glossary glossary)
    {
        foreach (var entry in glossary.Entries)
            Entries.Add(new GlossaryEntryViewModel { Source = entry.Source, Target = entry.Target, Note = entry.Note });
    }

    public ObservableCollection<GlossaryEntryViewModel> Entries { get; } = [];

    [RelayCommand]
    void Add() => Entries.Add(new GlossaryEntryViewModel());

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
