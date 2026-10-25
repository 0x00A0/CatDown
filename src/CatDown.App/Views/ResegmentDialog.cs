using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CatDown.App.Localization;
using CatDown.App.Services;
using CatDown.Core.Editing;

namespace CatDown.App.Views;

// Asks what to do with an already translated paragraph that is being split again.
public class ResegmentDialog : Window
{
    public ResegmentDialog(string paragraph, bool canRealign)
    {
        Title = Loc.Get("TranslatedBlockTitle");
        Width = 480;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var skip = new RadioButton { Content = Loc.Get("ActionSkip"), GroupName = "action" };
        var merge = new RadioButton { Content = Loc.Get("ActionMerge"), GroupName = "action", IsChecked = true };
        var realign = new RadioButton { Content = Loc.Get("ActionRealign"), GroupName = "action", IsEnabled = canRealign };
        var all = new CheckBox { Content = Loc.Get("ApplyToAll") };
        var ok = new Button { Content = Loc.Get("Ok"), HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (_, _) => Close(new ResegmentChoice(
            skip.IsChecked == true ? TranslatedBlockAction.Skip
            : realign.IsChecked == true ? TranslatedBlockAction.LlmRealign
            : TranslatedBlockAction.MergeIntoFirst,
            all.IsChecked == true));

        var quote = new Border
        {
            Padding = new Thickness(10, 6),
            CornerRadius = new CornerRadius(4),
            Child = new TextBlock
            {
                Text = paragraph.Length > 300 ? paragraph[..300] + "…" : paragraph,
                TextWrapping = TextWrapping.Wrap,
            },
        };
        quote.Bind(Border.BackgroundProperty, this.GetResourceObservable("SemiColorFill0"));

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 12,
            Children = { new TextBlock { Text = Loc.Get("TranslatedBlockQuestion"), TextWrapping = TextWrapping.Wrap }, quote, skip, merge, realign, all, ok },
        };
    }
}
