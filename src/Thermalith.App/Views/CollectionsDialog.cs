using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Thermalith.App.Services;
using Thermalith.App.ViewModels;
using Thermalith.Core.Serialization;

namespace Thermalith.App.Views;

public sealed partial class CollectionsDialog : Window
{
    private readonly MainWindowViewModel _vm;
    private readonly CollectionStore _store = new();
    private readonly TreeView _tree = new();
    private readonly WrapPanel _cards = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly List<Bitmap> _images = [];
    private string? _selected;
    private bool _rebuilding;
    private bool _showPaths;
    private readonly bool _insertBlocks;
    private const string BlocksKey = "@custom-blocks";
    private readonly Avalonia.Threading.DispatcherTimer _watch = new() { Interval = TimeSpan.FromSeconds(2) };
    private string _fileState = "";
    private string FileState() => BlockLibrary.Shared.Signature() + string.Join(";", _store.Catalog.Labels.Select(l => l.Id + ":" + (File.Exists(l.Path) ? File.GetLastWriteTimeUtc(l.Path).Ticks : -1)));

    public CollectionsDialog(MainWindowViewModel vm, bool showBlocks = false,bool insertBlocks = false)
    {
        _insertBlocks = insertBlocks;
        _selected = showBlocks ? BlocksKey : null;
        _vm = vm; Title = "Label collections"; Width = 1200; Height = 800; MinWidth = 800; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(12) };
        var tools = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,10) };
        void Tool(string text, Func<Task> action)
        {
            var button = new Button { Content = text, Margin = new Thickness(0,0,8,5) };
            button.Click += async (_,_) => { try { await action(); } catch (Exception ex) { _status.Text = ex.Message; } };
            tools.Children.Add(button);
        }
        Tool("New collection", () => Create(false)); Tool("New subcollection", () => Create(true));
        Tool("Add current design", AddCurrent); Tool("Add file…", AddFile);
        Tool("Block folder...", ChangeBlockFolder);
        Tool("Refresh", () => { Refresh(); return Task.CompletedTask; });
        Tool("Close", () => { Close(); return Task.CompletedTask; });
        var showPaths = new CheckBox { Content = "Show paths", Margin = new Thickness(6,0,0,5), VerticalAlignment = VerticalAlignment.Center };
        showPaths.IsCheckedChanged += (_,_) => { _showPaths = showPaths.IsChecked == true; ShowCards(); };
        tools.Children.Add(showPaths);
        DockPanel.SetDock(tools,Dock.Top); root.Children.Add(tools);
        DockPanel.SetDock(_status,Dock.Bottom); root.Children.Add(_status);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("260,6,*") };
        _tree.SelectionChanged += (_,_) => { if (!_rebuilding && _tree.SelectedItem is TreeViewItem item) { _selected = item.Tag as string; ShowCards(); } };
        WireCollectionDragging();
        grid.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(5), Child = _tree });
        var split = new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Stretch }; Grid.SetColumn(split,1); grid.Children.Add(split);
        var scroll = new ScrollViewer { Content = _cards, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetColumn(scroll,2); grid.Children.Add(scroll); root.Children.Add(grid); Content = root;
        _watch.Tick += (_,_) => { try { if (FileState() != _fileState) Refresh(); } catch (Exception ex) { _status.Text = ex.Message; } };
        Closed += (_,_) => { _watch.Stop(); foreach (var image in _images) image.Dispose(); };
        Opened += (_,_) => { Refresh(); _watch.Start(); };
    }
    private void WireCollectionDragging()
    {
        string? source = null; Point start = default; bool dragging = false;
        TreeViewItem? ItemAt(Point position)
        {
            var visual = _tree.InputHitTest(position) as Visual;
            return visual as TreeViewItem ?? visual?.GetVisualAncestors().OfType<TreeViewItem>().FirstOrDefault();
        }
        _tree.AddHandler(PointerPressedEvent, (_,e) =>
        {
            if (!e.GetCurrentPoint(_tree).Properties.IsLeftButtonPressed) return;
            start = e.GetPosition(_tree); source = ItemAt(start)?.Tag as string; if (source == BlocksKey) source = null; dragging = false;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _tree.PointerMoved += (_,e) =>
        {
            if (source is null || !e.GetCurrentPoint(_tree).Properties.IsLeftButtonPressed) return;
            var point = e.GetPosition(_tree);
            if (!dragging && Math.Abs(point.X - start.X) + Math.Abs(point.Y - start.Y) < 6) return;
            dragging = true; e.Pointer.Capture(_tree);
            var target = ItemAt(point);
            _status.Text = target is null ? "Drop on a collection, or All collections to move to root." : "Drop in the middle to nest; at the top/bottom edge to reorder.";
        };
        _tree.PointerReleased += (_,e) =>
        {
            var id = source; source = null;
            if (!dragging || id is null) return;
            dragging = false; e.Pointer.Capture(null); e.Handled = true;
            var target = ItemAt(e.GetPosition(_tree)); if (target is null) return;
            var targetId = target.Tag as string;
            if (targetId == BlocksKey) { _status.Text = "Regular collections cannot be moved into the block library."; return; }
            try
            {
                string? parent = targetId; string? before = null;
                if (targetId is not null)
                {
                    var local = e.GetPosition(target);
                    // Use the header height, not the expanded subtree height.
                    if (local.Y < 8 || local.Y > 24)
                    {
                        var item = _store.Catalog.Collections.Single(c => c.Id == targetId); parent = item.ParentId;
                        if (local.Y < 8) before = targetId;
                        else before = _store.Catalog.Collections.Where(c => c.ParentId == parent).SkipWhile(c => c.Id != targetId).Skip(1).FirstOrDefault()?.Id;
                    }
                }
                _store.Move(id,parent,before); _selected = id; Refresh();
            }
            catch (Exception ex) { _status.Text = ex.Message; }
        };
        _tree.PointerCaptureLost += (_,e) => { if (e.Pointer.Captured != _tree) { source = null; dragging = false; } };
    }

    private async Task Create(bool child)
    {
        if (_selected == BlocksKey) { _status.Text = "Select a regular collection to add a subcollection."; return; }
        if (child && _selected is null) { _status.Text = "Select a parent collection first."; return; }
        var name = await Prompt("New collection", "Collection name");
        if (name is null) return;
        _selected = _store.Create(name, child ? _selected : null).Id; Refresh();
    }
    private async Task AddCurrent()
    {
        if (_selected == BlocksKey) { await _vm.SaveAsCustomBlockCommand.ExecuteAsync(null); Refresh(); return; }
        if (_selected is null) { _status.Text = "Select a collection first."; return; }
        if (_vm.Editor.FilePath is null || _vm.Editor.Dirty)
        {
            if (!await _vm.Dialogs!.ConfirmAsync("Save design", "Save this design before adding it to the collection?", "Save")) return;
            await _vm.SaveCommand.ExecuteAsync(null);
            if (_vm.Editor.FilePath is null || _vm.Editor.Dirty) return;
        }
        AddPath(_vm.Editor.FilePath!);
    }
    private async Task AddFile()
    {
        if (_selected is null) { _status.Text = "Select a collection first."; return; }
        var path = await _vm.FilePicker!.OpenLabelAsync(); if (path is not null) AddPath(path);
    }
    private void AddPath(string path)
    {
        // Check that this is a readable label before storing its reference.
        var package = LabelPackageIo.Load(path);
        if (_selected == BlocksKey) { BlockLibrary.Shared.Create(package); Refresh(); return; }
        var label = _store.Add(_selected!,path);
        Cache(label); Refresh();
    }
    private void Cache(CollectionLabel label)
    {
        if (!File.Exists(label.Path)) return;
        var stamp = File.GetLastWriteTimeUtc(label.Path).Ticks;
        if (File.Exists(_store.ThumbnailPath(label.Id)) && label.CachedWriteTime == stamp && label.ThumbnailVersion == 1) return;
        var editor = new EditorViewModel(); editor.LoadPackage(LabelPackageIo.Load(label.Path),label.Path);
        using var bitmap = editor.RenderPrintPreview(0,false,applyOrientation: false);
        _store.Cache(label, stream => bitmap.Save(stream));
    }
    private void Refresh()
    {
        _rebuilding = true;
        var roots = new List<TreeViewItem>(); TreeViewItem? selected = null;
        TreeViewItem Node(LabelCollection? collection)
        {
            var id = collection?.Id;
            var node = new TreeViewItem { Header = $"{collection?.Name ?? "All collections"} ({_store.Members(id).Count()})", Tag = id, IsExpanded = true };
            if (id == _selected) selected = node;
            node.ItemsSource = _store.Catalog.Collections.Where(c => c.ParentId == id).Select(Node).ToArray();
            return node;
        }
        roots.Add(Node(null));
        var blocksNode = new TreeViewItem { Header = "Custom blocks (" + BlockLibrary.Shared.Catalog.Blocks.Count + ")", Tag = BlocksKey, Foreground = Brushes.Turquoise, FontWeight = FontWeight.Bold };
        roots.Add(blocksNode); if (_selected == BlocksKey) selected = blocksNode;
        _tree.ItemsSource = roots; _tree.SelectedItem = selected ?? roots[0]; _rebuilding = false; ShowCards();
    }
    private void ShowCards()
    {
        _cards.Children.Clear(); foreach (var image in _images) image.Dispose(); _images.Clear();
        if (_selected == BlocksKey) { ShowBlocks(); return; }
        var labels = _store.Members(_selected).OrderBy(l => l.Name).ToArray(); var missing = 0;
        foreach (var label in labels)
        {
            var exists = File.Exists(label.Path); if (!exists) missing++;
            string? error = null; try { Cache(label); } catch (Exception ex) { error = ex.Message; }
            var stack = new StackPanel { Spacing = 7 };
            var cache = _store.ThumbnailPath(label.Id);
            var preview = new Border { Height = 210, Background = Brushes.White, Padding = new Thickness(8) };
            if (File.Exists(cache))
            {
                try { var image = new Bitmap(cache); _images.Add(image); preview.Child = new Image { Source = image, Stretch = Stretch.Uniform }; }
                catch { preview.Child = new TextBlock { Text = "Preview unavailable", Foreground = Brushes.Black }; }
            }
            else preview.Child = new TextBlock { Text = "Preview unavailable", Foreground = Brushes.Black };
            stack.Children.Add(preview);
            var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var name = new TextBlock { Text = label.Name, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,6,0) };
            ToolTip.SetTip(name,label.Name); heading.Children.Add(name); stack.Children.Add(heading);
            if (_showPaths)
            {
                var pathLink = new Button
                {
                    Content = new TextBlock { Text = label.Path, TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Brushes.DeepSkyBlue },
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                ToolTip.SetTip(pathLink,"Open file location in Windows Explorer");
                pathLink.Click += (_,_) =>
                {
                    try
                    {
                        var start = new System.Diagnostics.ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                        if (File.Exists(label.Path))
                        {
                            start.Arguments = "/select,\"" + label.Path + "\"";
                        }
                        else
                        {
                            var folder = Path.GetDirectoryName(label.Path);
                            while (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) folder = Path.GetDirectoryName(folder);
                            if (string.IsNullOrEmpty(folder)) { _status.Text = "The original file location is unavailable."; return; }
                            start.ArgumentList.Add(folder);
                        }
                        System.Diagnostics.Process.Start(start);
                    }
                    catch (Exception ex) { _status.Text = "Cannot open file location: " + ex.Message; }
                };
                stack.Children.Add(pathLink);
            }
            if (!exists) stack.Children.Add(new TextBlock { Text = "Missing file — cached preview retained. Restore the file or remove its collection reference.", Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap });
            if (error is not null) stack.Children.Add(new TextBlock { Text = "Cannot refresh preview: " + error, Foreground = Brushes.Orange, TextWrapping = TextWrapping.Wrap });
            async Task OpenDesign()
            {
                if (!File.Exists(label.Path)) { Refresh(); return; }
                await _vm.OpenRecentCommand.ExecuteAsync(label.Path);
                if (string.Equals(_vm.Editor.FilePath,label.Path,StringComparison.OrdinalIgnoreCase)) Close();
            }
            preview.DoubleTapped += async (_,e) => { e.Handled = true; await OpenDesign(); };
            ToolTip.SetTip(preview, exists ? "Double-click to open design" : "File missing; cached preview retained");
            var open = new MenuItem { Header = "Open", IsEnabled = exists };
            open.Click += async (_,_) => await OpenDesign();
            var remove = new MenuItem { Header = "Remove from collection..." };
            remove.Click += async (_,_) => await Remove(label);
            var menu = new ContextMenu { ItemsSource = new MenuItem[] { open, remove } };
            var more = new Button { Content = new Material.Icons.Avalonia.MaterialIcon { Kind = Material.Icons.MaterialIconKind.DotsVertical }, HorizontalAlignment = HorizontalAlignment.Right, ContextMenu = menu };
            Avalonia.Automation.AutomationProperties.SetName(more,"Design actions");
            ToolTip.SetTip(more,"Design actions"); more.Click += (_,_) => menu.Open(more);
            Grid.SetColumn(more,1); heading.Children.Add(more);
            _cards.Children.Add(new Border { Width = 350, Margin = new Thickness(8), Padding = new Thickness(10), BorderThickness = new Thickness(1), BorderBrush = exists ? Brushes.Gray : Brushes.Orange, CornerRadius = new CornerRadius(5), Child = stack });
        }
        _fileState = FileState();
        _status.Text = $"{labels.Length} design(s); {missing} missing. Counts include subcollections; each design is counted once. Collection operations never delete files.";
    }
    private async Task Remove(CollectionLabel label)
    {
        var memberships = _store.Catalog.Collections.Where(c => c.Labels.Contains(label.Id)).ToArray();
        var dialog = new Window { Title = "Remove from collection", Width = 480, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(18), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = $"Remove {label.Name} from a collection? The design file will remain on disk.", TextWrapping = TextWrapping.Wrap });
        var choice = new ComboBox { ItemsSource = memberships.Select(c => CollectionPath(c)).ToArray(), SelectedIndex = Math.Max(0,Array.FindIndex(memberships,c => c.Id == _selected)), HorizontalAlignment = HorizontalAlignment.Stretch };
        var all = new CheckBox { Content = "Remove from all collections" }; all.IsCheckedChanged += (_,_) => choice.IsEnabled = all.IsChecked != true;
        panel.Children.Add(choice); panel.Children.Add(all);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", IsCancel = true }; cancel.Click += (_,_) => dialog.Close(false);
        var remove = new Button { Content = "Remove" }; remove.Click += (_,_) => dialog.Close(true);
        buttons.Children.Add(cancel); buttons.Children.Add(remove); panel.Children.Add(buttons); dialog.Content = panel;
        if (await dialog.ShowDialog<bool>(this)) { _store.Remove(memberships[choice.SelectedIndex].Id,label.Id,all.IsChecked == true); Refresh(); }
    }
    private string CollectionPath(LabelCollection collection)
    {
        var parts = new List<string>(); var seen = new HashSet<string>(); LabelCollection? current = collection;
        while (current is not null && seen.Add(current.Id)) { parts.Insert(0,current.Name); current = _store.Catalog.Collections.FirstOrDefault(c => c.Id == current.ParentId); }
        return string.Join(" / ",parts);
    }
    private async Task<string?> Prompt(string title, string caption)
    {
        var dialog = new Window { Title = title, Width = 400, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(18), Spacing = 12 };
        var text = new TextBox { Watermark = caption }; panel.Children.Add(text);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", IsCancel = true }; cancel.Click += (_,_) => dialog.Close((string?)null);
        var create = new Button { Content = "Create", IsDefault = true }; create.Click += (_,_) => { if (!string.IsNullOrWhiteSpace(text.Text)) dialog.Close(text.Text.Trim()); };
        buttons.Children.Add(cancel); buttons.Children.Add(create); panel.Children.Add(buttons); dialog.Content = panel;
        return await dialog.ShowDialog<string?>(this);
    }
}
