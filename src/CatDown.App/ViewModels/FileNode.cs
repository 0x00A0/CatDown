using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CatDown.App.ViewModels;

// A folder or Markdown file in the project tree.
public partial class FileNode : ObservableObject
{
    public required string Name { get; init; }

    // Null for folders.
    public string? RelativePath { get; init; }

    public bool IsFile => RelativePath is not null;

    public ObservableCollection<FileNode> Children { get; } = [];

    [ObservableProperty]
    private int? _progress;

    public string ProgressText => Progress is { } p ? $"{p}%" : "";

    partial void OnProgressChanged(int? value) => OnPropertyChanged(nameof(ProgressText));

    // Builds a tree from sorted '/'-separated relative paths. Folders come before files.
    public static ObservableCollection<FileNode> BuildTree(IEnumerable<string> paths, Func<string, int?> progress)
    {
        var root = new FileNode { Name = "" };
        foreach (var path in paths)
        {
            var parts = path.Split('/');
            var parent = root;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var folder = parent.Children.FirstOrDefault(c => !c.IsFile && c.Name == parts[i]);
                if (folder is null)
                {
                    folder = new FileNode { Name = parts[i] };
                    parent.Children.Add(folder);
                }
                parent = folder;
            }
            parent.Children.Add(new FileNode { Name = parts[^1], RelativePath = path, Progress = progress(path) });
        }
        Sort(root);
        return root.Children;
    }

    static void Sort(FileNode node)
    {
        var sorted = node.Children.OrderBy(c => c.IsFile).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        node.Children.Clear();
        foreach (var child in sorted)
        {
            Sort(child);
            node.Children.Add(child);
        }
    }

    public IEnumerable<FileNode> Flatten() => Children.SelectMany(c => c.Flatten().Prepend(c));
}
