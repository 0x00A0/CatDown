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
        var match = e.Key switch
        {
            Key.D1 or Key.NumPad1 => 1,
            Key.D2 or Key.NumPad2 => 2,
            Key.D3 or Key.NumPad3 => 3,
            _ => 0,
        };
        if (match > 0 && !shift)
        {
            vm.UseMatchCommand.Execute(match);
            e.Handled = true;
            return;
        }

        System.Windows.Input.ICommand? command = (e.Key, shift) switch
        {
            (Key.Enter, false) => vm.ConfirmCommand,
            (Key.C, true) => vm.CopySourceCommand,
            (Key.J, true) => vm.MergeCommand,
            (Key.K, true) => vm.SplitCommand,
            (Key.M, true) => vm.MachineTranslateCommand,
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
        // Ctrl/Cmd- and Shift-clicks extend the selection; let the list handle those.
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) || Shortcuts.IsCommand(e.KeyModifiers))
            return;
        if (Vm is { } vm && e.Source is Control source
            && source.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is SegmentRowViewModel row)
        {
            vm.SelectedRow = row;
        }
    }
}
