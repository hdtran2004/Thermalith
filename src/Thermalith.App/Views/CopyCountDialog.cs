using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Thermalith.App.Views;

public sealed record CopyCountConfirmation(bool Confirmed, bool SkipForSession);

public sealed class CopyCountDialog : Window
{
    public CopyCountDialog(int copies)
    {
        Title = "Confirm print quantity";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        var skip = new CheckBox { Content = "Don't ask again during this session" };
        var cancel = new Button { Content = "Cancel", MinWidth = 85, IsCancel = true };
        var print = new Button { Content = $"Print {copies} copies", MinWidth = 120 };
        cancel.Click += (_, _) => Close(new CopyCountConfirmation(false, false));
        print.Click += (_, _) => Close(new CopyCountConfirmation(true, skip.IsChecked == true));
        Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 16,
            Children =
            {
                new TextBlock { Text = $"You have selected {copies} copies. Continue printing?", TextWrapping = TextWrapping.Wrap },
                skip,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8, Children = { cancel, print } },
            },
        };
    }
}
