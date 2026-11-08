using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Material.Icons;
using Material.Icons.Avalonia;
using Thermalith.App.ViewModels;
namespace Thermalith.App.Views;
public sealed class TableBorderControls : UserControl
{
    public TableBorderControls()
    {
        var root = new StackPanel { Spacing = 5 };
        var title = new TextBlock { Text = "Borders" }; title.Classes.Add("section"); root.Children.Add(title);
        var widthRow = new Grid { ColumnDefinitions = new ColumnDefinitions("90,*") };
        widthRow.Children.Add(new TextBlock { Text = "Width mm", VerticalAlignment = VerticalAlignment.Center });
        var width = new NumericUpDown { Minimum = 0.05m, Maximum = 5, Increment = 0.05m, FormatString = "0.##" };
        width.Bind(NumericUpDown.ValueProperty,new Binding("Editor.CellBorderWidthMm") { Mode = BindingMode.TwoWay });
        Grid.SetColumn(width,1); widthRow.Children.Add(width); root.Children.Add(widthRow);
        var styleRow = new Grid { ColumnDefinitions = new ColumnDefinitions("90,*") };
        styleRow.Children.Add(new TextBlock { Text = "Line style", VerticalAlignment = VerticalAlignment.Center });
        var style = new ComboBox { ItemsSource = new[] { "solid", "dashed", "dotted" }, HorizontalAlignment = HorizontalAlignment.Stretch };
        style.Bind(ComboBox.SelectedItemProperty,new Binding("Editor.CellBorderStyle") { Mode = BindingMode.TwoWay });
        Grid.SetColumn(style,1); styleRow.Children.Add(style); root.Children.Add(styleRow);
        var presets = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (key,label,icon) in new[] { ("all","All borders",MaterialIconKind.BorderAll), ("outer","Outside borders",MaterialIconKind.BorderOutside), ("inner","Inside borders",MaterialIconKind.BorderInside), ("top","Top border",MaterialIconKind.BorderTop), ("bottom","Bottom border",MaterialIconKind.BorderBottom), ("left","Left border",MaterialIconKind.BorderLeft), ("right","Right border",MaterialIconKind.BorderRight), ("none","No borders",MaterialIconKind.BorderNone) })
        {
            var button = new Button { Content = new MaterialIcon { Kind = icon }, Margin = new Thickness(1) }; button.Classes.Add("icon");
            ToolTip.SetTip(button,label);
            button.Click += (_,_) => { if (DataContext is MainWindowViewModel vm) vm.Editor.ApplyTableBorders(key); };
            presets.Children.Add(button);
        }
        root.Children.Add(presets); Content = root;
    }
}
