using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;
using Thermalith.Core.Model;

namespace Thermalith.App.Views;

/// <summary>Draft parameter rows. Apply commits the complete list; Cancel leaves the design untouched.</summary>
public sealed class ParametersDialog : Window
{
    private const string Columns = "150,150,140,120,64,220,90,40";
    private Grid? _header;
    private readonly Dictionary<int,double> _manualWidths = [];
    private static readonly double[] MinimumWidths = [140,140,140,110,64,180,90,40];
    private readonly StackPanel _rows = new() { Spacing = 3 };
    private readonly List<Row> _entries = [];
    private bool _advancedOpen;
    private readonly IReadOnlySet<string> _lockedNames;
    private readonly TextBlock _error = new() { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
    private sealed record Row(Border View, Func<LabelParameter> Read, Action Refresh);

    public ParametersDialog(IEnumerable<LabelParameter> parameters,IReadOnlySet<string>? lockedNames = null)
    {
        _lockedNames = lockedNames ?? new HashSet<string>();
        Title = "Label parameters"; Width = 1140; Height = 480; MinWidth = 780; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(18) };
        var help = new TextBlock
        {
            Text = "Edit common settings directly below. Double-click a row or choose Advanced for numbering format, step and digit padding.\nUse {Name} in your design or inside another parameter prefix/suffix. Drag column dividers to resize; double-click a divider to fit. Next number is remembered after printing; edit it to override. A and I both start at 1.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,14)
        };
        DockPanel.SetDock(help, Dock.Top); root.Children.Add(help);
        var footer = new StackPanel { Spacing = 8, Margin = new Thickness(0,12,0,0) };
        footer.Children.Add(_error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        var addContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        addContent.Children.Add(new MaterialIcon { Kind = MaterialIconKind.FunctionVariant });
        addContent.Children.Add(new TextBlock { Text = "Add parameter", VerticalAlignment = VerticalAlignment.Center });
        var add = new Button { Content = addContent };
        add.Click += (_, _) =>
        {
            var names = _entries.Select(row => row.Read().Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var number = 1; while (names.Contains("Parameter" + number)) number++;
            Add(new LabelParameter { Name = "Parameter" + number });
        };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => Close((List<LabelParameter>?)null);
        var apply = new Button { Content = "Apply", IsDefault = true };
        apply.Click += (_, _) =>
        {
            try
            {
                var result = _entries.Select(row => row.Read()).ToList();
                foreach (var p in result) p.Validate();
                if (result.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Count)
                    throw new ArgumentException("Each parameter must have a unique name.");
                Thermalith.Core.Tokens.ParameterResolver.ResolveAll(result);
                Close(result);
            }
            catch (Exception ex) { _error.Text = ex.Message; }
        };
        actions.Children.Add(add); actions.Children.Add(cancel); actions.Children.Add(apply);
        footer.Children.Add(actions); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var table = new StackPanel { Spacing = 6 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns), Margin = new Thickness(7,0,7,3) };
        _header = header;
        var headings = new[] { "Name", "Fixed prefix", "Next number", "Fixed suffix", "Auto", "Next value", "Settings", "" };
        for (var i = 0; i < headings.Length; i++)
        {
            var label = new TextBlock { Text = headings[i], FontWeight = FontWeight.SemiBold, Margin = new Thickness(4,0) };
            Grid.SetColumn(label,i); header.Children.Add(label);
            var column = i;
            var handle = new Border { Width = 7, HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.SizeWestEast) };
            ToolTip.SetTip(handle,"Drag to resize. Double-click to fit content.");
            var resizing = false; double origin = 0, initial = 0;
            handle.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
                resizing = true; origin = e.GetPosition(this).X; initial = header.ColumnDefinitions[column].ActualWidth;
                e.Pointer.Capture(handle); e.Handled = true;
            };
            handle.PointerMoved += (_, e) =>
            {
                if (!resizing) return;
                _manualWidths[column] = Math.Max(MinimumWidths[column], initial + e.GetPosition(this).X - origin);
                UpdateColumnWidths(); e.Handled = true;
            };
            handle.PointerReleased += (_, e) => { resizing = false; e.Pointer.Capture(null); e.Handled = true; };
            handle.DoubleTapped += (_, e) => { _manualWidths.Remove(column); UpdateColumnWidths(); e.Handled = true; };
            Grid.SetColumn(handle,i); header.Children.Add(handle);
        }
        table.Children.Add(header); table.Children.Add(_rows);
        root.Children.Add(new ScrollViewer { Content = table, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        Content = root;
        SizeChanged += (_,_) => UpdateColumnWidths();
        foreach (var parameter in parameters) Add(parameter);
    }

    private void Add(LabelParameter parameter)
    {
        var row = BuildRow(parameter);
        _entries.Add(row); _rows.Children.Add(row.View); RefreshPreviews();
    }

    private void RefreshPreviews() { foreach (var row in _entries) row.Refresh(); UpdateColumnWidths(); }

    private void UpdateColumnWidths()
    {
        if (_header is null) return;
        var desired = (double[])MinimumWidths.Clone();
        double TextWidth(string text)
        {
            var measure = new TextBlock { Text = text, FontSize = 13 };
            measure.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
            return measure.DesiredSize.Width + 30;
        }
        foreach (var entry in _entries)
        {
            var p = entry.Read();
            desired[0] = Math.Max(desired[0],Math.Min(300,TextWidth(p.Name)));
            desired[1] = Math.Max(desired[1],Math.Min(500,TextWidth(p.Prefix)));
            desired[2] = Math.Max(desired[2],TextWidth(p.Start.ToString()) + 64);
            desired[3] = Math.Max(desired[3],Math.Min(400,TextWidth(p.Suffix)));
            var grid = (Grid)entry.View.Child!;
            var preview = grid.Children.OfType<TextBlock>().First();
            desired[5] = Math.Max(desired[5],Math.Min(700,TextWidth(preview.Text ?? "")));
        }
        var widths = (double[])MinimumWidths.Clone();
        foreach (var (column,width) in _manualWidths) widths[column] = width;
        var automatic = Enumerable.Range(0,8).Where(c=>!_manualWidths.ContainsKey(c)).ToArray();
        var available = Math.Max(widths.Sum(), (Bounds.Width > 0 ? Bounds.Width : Width) - 70);
        var budget = Math.Max(0,available-widths.Sum());
        var need = automatic.Sum(c=>desired[c]-widths[c]);
        foreach (var c in automatic) widths[c] += need <= budget ? desired[c]-widths[c] : need > 0 ? (desired[c]-widths[c])*budget/need : 0;
        var extra = Math.Max(0,available-widths.Sum());
        var flexible = new[] { 1,3,5 }.Where(c=>!_manualWidths.ContainsKey(c)).ToArray();
        foreach (var c in flexible) widths[c] += extra / flexible.Length;
        void Apply(Grid grid)
        {
            for (var c = 0; c < 8; c++)
                if (Math.Abs(grid.ColumnDefinitions[c].Width.Value-widths[c])>0.5) grid.ColumnDefinitions[c].Width = new GridLength(widths[c]);
        }
        Apply(_header);
        foreach (var entry in _entries) Apply((Grid)entry.View.Child!);
    }
    private Row BuildRow(LabelParameter parameter)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(Columns) };
        var border = new Border { Child = grid, Padding = new Thickness(6), BorderBrush = Brushes.DimGray, BorderThickness = new Thickness(0,0,0,1) };
        void Put(Control control, int column) { control.Margin = new Thickness(3,0); control.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(control,column); grid.Children.Add(control); }
        var name = new TextBox { Text = parameter.Name, IsReadOnly = _lockedNames.Contains(parameter.Name) };
        if (name.IsReadOnly)
        {
            name.Foreground = Brushes.DeepSkyBlue;
            ToolTip.SetTip(name,"Inherited name: unlock the custom block to rename it. Values are editable in this design.");
        } Put(name,0);
        var prefix = new TextBox { Text = parameter.Prefix }; Put(prefix,1);
        var next = new NumericUpDown { Minimum = 1, Maximum = long.MaxValue, Value = parameter.Start, FormatString = "0" }; Put(next,2);
        var suffix = new TextBox { Text = parameter.Suffix }; Put(suffix,3);
        var enabled = new CheckBox { IsChecked = parameter.IncrementEnabled, HorizontalAlignment = HorizontalAlignment.Center }; Put(enabled,4);
        var preview = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis }; Put(preview,5);
        LabelParameter Read() => parameter.WithName(name.Text?.Trim() ?? "") with { Prefix = prefix.Text ?? "", Suffix = suffix.Text ?? "", Start = checked((long)(next.Value ?? 1)), IncrementEnabled = enabled.IsChecked == true };
        var entry = new Row(border, Read, Update);
        void Update()
        {
            try { var draft = Read(); var all = _entries.Where(row => !ReferenceEquals(row.View, border)).Select(row => row.Read()).Append(draft); var value = Thermalith.Core.Tokens.ParameterResolver.ResolveAll(all)[draft.Name]; preview.Text = value.Length == 0 ? "(empty)" : value; ToolTip.SetTip(preview,value); }
            catch (Exception ex) { preview.Text = "Invalid value"; ToolTip.SetTip(preview,ex.Message); }
            next.IsEnabled = enabled.IsChecked == true;
        }
        async Task Advanced()
        {
            if (_advancedOpen) return;
            _advancedOpen = true;
            try
            {
                var value = Read(); value.Validate();
                var result = await new ParameterAdvancedDialog(value, _entries.Where(row => row != entry).Select(row => row.Read()).ToList(), _lockedNames.Contains(parameter.Name)).ShowDialog<LabelParameter?>(this);
                if (result is null) return;
                var index = _entries.IndexOf(entry);
                if (index < 0) return;
                var replacement = BuildRow(result);
                _entries[index] = replacement; _rows.Children[index] = replacement.View; RefreshPreviews();
                _error.Text = "";
            }
            catch (Exception ex) { _error.Text = ex.Message; }
            finally { _advancedOpen = false; }
        }
        var advanced = new Button { Content = "Advanced", Padding = new Thickness(6,4) }; Put(advanced,6);
        advanced.Click += async (_, _) => await Advanced();
        border.DoubleTapped += async (_, e) => { e.Handled = true; await Advanced(); };
        var remove = new Button { IsEnabled = !_lockedNames.Contains(parameter.Name), Content = new MaterialIcon { Kind = MaterialIconKind.DeleteOutline }, Padding = new Thickness(5) }; Put(remove,7);
        ToolTip.SetTip(remove,"Remove parameter");
        remove.Click += (_, _) => { _entries.Remove(entry); _rows.Children.Remove(border); RefreshPreviews(); };
        foreach (var box in new[] { name, prefix, suffix }) box.TextChanged += (_, _) => RefreshPreviews();
        next.ValueChanged += (_, _) => RefreshPreviews(); enabled.IsCheckedChanged += (_, _) => RefreshPreviews();
        Update();
        return entry;
    }
}
