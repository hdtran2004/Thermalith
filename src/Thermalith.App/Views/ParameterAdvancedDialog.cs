using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Thermalith.Core.Model;

namespace Thermalith.App.Views;

public sealed class ParameterAdvancedDialog : Window
{
    private readonly List<LabelParameter> _context;
    private readonly bool _nameReadOnly;
    private readonly StackPanel _rows = new() { Spacing = 14 };
    private readonly List<Func<LabelParameter>> _readers = [];
    private readonly TextBlock _error = new() { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };

    public ParameterAdvancedDialog(LabelParameter parameter, IEnumerable<LabelParameter>? context = null,bool nameReadOnly = false)
    {
        _context = (context ?? []).ToList();
        _nameReadOnly = nameReadOnly;
        Title = "Advanced parameter settings"; Width = 620; Height = 660;
        MinWidth = 540; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(18), LastChildFill = true };
        var help = new TextBlock { Text = "Use {Name} in text, QR values, barcodes or table cells. Parameters are saved with this label.\nNext value is remembered after each confirmed printed label, including copies. Edit Next to override it. Alphabetic: A … Z, AA …; Roman: I, II, III … (parentheses indicate thousands above 3999).", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,14) };
        DockPanel.SetDock(help, Dock.Top); root.Children.Add(help);
        var buttons = new StackPanel { Spacing = 8 };
        buttons.Children.Add(_error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel" }; cancel.Click += (_, _) => Close((LabelParameter?)null);
        var save = new Button { Content = "Apply", IsDefault = true };
        save.Click += (_, _) =>
        {
            try
            {
                var result = _readers.Select(read => read()).ToList();
                foreach (var parameter in result) parameter.Validate();
                if (result.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Count)
                    throw new ArgumentException("Each parameter must have a unique name.");
                Thermalith.Core.Tokens.ParameterResolver.ResolveAll(_context.Append(result[0]));
                Close(result[0]);
            }
            catch (Exception ex) { _error.Text = ex.Message; }
        };
        actions.Children.Add(cancel); actions.Children.Add(save);
        buttons.Children.Add(actions); DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        root.Children.Add(new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        Content = root;
        AddRow(parameter);
    }

    private void AddRow(LabelParameter parameter)
    {
        var panel = new StackPanel { Spacing = 7 };
        var border = new Border { Child = panel, Padding = new Thickness(12), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5) };
        TextBox Field(string label, string value)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("100,*") };
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            var box = new TextBox { Text = value }; Grid.SetColumn(box, 1); row.Children.Add(box); panel.Children.Add(row); return box;
        }
        var name = Field("Name", parameter.Name); name.IsReadOnly = _nameReadOnly;
        var prefix = Field("Fixed prefix", parameter.Prefix);
        var suffix = Field("Fixed suffix", parameter.Suffix);
        var enabled = new CheckBox { Content = "Enable auto increment", IsChecked = parameter.IncrementEnabled }; panel.Children.Add(enabled);
        var format = new ComboBox { ItemsSource = new[] { "Numeric", "Alphabetic", "Roman" }, SelectedItem = parameter.Format, HorizontalAlignment = HorizontalAlignment.Stretch }; panel.Children.Add(format);
        NumericUpDown Number(string label, long value, long max)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("100,*") };
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            var box = new NumericUpDown { Minimum = 1, Maximum = max, Value = value, FormatString = "0" }; Grid.SetColumn(box, 1); row.Children.Add(box); panel.Children.Add(row); return box;
        }
        var start = Number("Next (A/I = 1)", parameter.Start, long.MaxValue);
        var step = Number("Step", parameter.Step, int.MaxValue);
        var padding = Number("Min. digits", parameter.Padding, 20);
        LabelParameter Read() => parameter.WithName(name.Text?.Trim() ?? "") with { Prefix = prefix.Text ?? "", Suffix = suffix.Text ?? "", IncrementEnabled = enabled.IsChecked == true, Format = format.SelectedItem as string ?? "Numeric", Start = checked((long)(start.Value ?? 1)), Step = checked((int)(step.Value ?? 1)), Padding = checked((int)(padding.Value ?? 1)) };
        Func<LabelParameter> reader = Read; _readers.Add(reader);
        var sample = new TextBlock { TextWrapping = TextWrapping.Wrap };
        void Preview()
        {
            try { var p = Read(); sample.Text = "{" + p.Name + "}:  " + string.Join("  →  ", Enumerable.Range(0, 3).Select(index => Thermalith.Core.Tokens.ParameterResolver.ResolveAll(_context.Append(p), index)[p.Name])); }
            catch { sample.Text = "Check the parameter settings."; }
            start.IsEnabled = step.IsEnabled = format.IsEnabled = enabled.IsChecked == true;
            padding.IsEnabled = enabled.IsChecked == true && format.SelectedItem as string == "Numeric";
        }
        foreach (var box in new[] { name, prefix, suffix }) box.TextChanged += (_, _) => Preview();
        foreach (var box in new[] { start, step, padding }) box.ValueChanged += (_, _) => Preview();
        format.SelectionChanged += (_, _) => Preview(); enabled.IsCheckedChanged += (_, _) => Preview();
        panel.Children.Add(sample);
        _rows.Children.Add(border); Preview();
    }
}
