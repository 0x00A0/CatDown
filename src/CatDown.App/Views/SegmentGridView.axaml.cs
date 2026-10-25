using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CatDown.App.Editor;
using CatDown.App.ViewModels;

namespace CatDown.App.Views;

public partial class SegmentGridView : UserControl
{
    public SegmentGridView()
    {
        InitializeComponent();
        // Tunnel so the editor does not swallow the shortcuts first.
        AddHandler(KeyDownEvent, OnShortcut, RoutingStrategies.Tunnel);
        // Clicking into a row's source text selects the row, so the split point belongs to it.
        RowList.AddHandler(PointerPressedEvent, OnRowPressed, RoutingStrategies.Tunnel);
    }

    SegmentGridViewModel? Vm => DataContext as SegmentGridViewModel;

    void OnShortcut(object? sender, KeyEventArgs e)
    {
        if (Vm is not { } vm || !Shortcuts.IsCommand(e.KeyModifiers))
            return;
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var command = (e.Key, shift) switch
        {
            (Key.Enter, false) => vm.ConfirmCommand,
            (Key.C, true) => vm.CopySourceCommand,
            (Key.J, true) => vm.MergeCommand,
            (Key.K, true) => vm.SplitCommand,
            _ => null,
        };
        if (command is null)
            return;
        command.Execute(null);
        e.Handled = true;
    }

    void OnSourceDoubleTapped(object? sender, TappedEventArgs e) => Vm?.EditSourceCommand.Execute(null);

    void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is { } vm && e.Source is Control source
            && source.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is SegmentRowViewModel row)
        {
            vm.SelectedRow = row;
        }
    }
}
