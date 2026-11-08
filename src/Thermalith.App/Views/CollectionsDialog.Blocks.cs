using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Thermalith.App.Services;
using Thermalith.App.ViewModels;
namespace Thermalith.App.Views;
public sealed partial class CollectionsDialog
{
    private async Task ChangeBlockFolder()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Custom block storage folder", AllowMultiple = false });
        var folder = folders.FirstOrDefault()?.TryGetLocalPath(); if (folder is null) return;
        if (_vm.Editor.IsBlockSource && _vm.Editor.Dirty) { _status.Text = "Save the open custom block before changing its folder."; return; }
        if (!await ConfirmBlockAction("Change custom block folder","Move all library block files to:\n" + folder + "\n\nLinked designs keep their block references.","Move blocks")) return;
        BlockLibrary.Shared.ChangeFolder(folder);
        if (_vm.Editor.CustomBlockId is { } id)
            _vm.Editor.LoadPackage(BlockLibrary.Shared.Load(id),BlockLibrary.Shared.PathFor(id));
        Refresh();
    }
    private async Task<bool> ConfirmBlockAction(string title,string message,string accept)
    {
        var window = new Window { Title = title,Width = 550,MaxHeight = 650,SizeToContent = SizeToContent.Height,WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var root = new StackPanel { Margin = new Thickness(18),Spacing = 14 };
        root.Children.Add(new ScrollViewer { MaxHeight = 450,Content = new TextBlock { Text = message,TextWrapping = TextWrapping.Wrap } });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal,Spacing = 8,HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel",IsCancel = true }; cancel.Click += (_,_) => window.Close(false);
        var confirm = new Button { Content = accept }; confirm.Click += (_,_) => window.Close(true);
        buttons.Children.Add(cancel); buttons.Children.Add(confirm); root.Children.Add(buttons); window.Content = root;
        return await window.ShowDialog<bool>(this);
    }
    private void ShowBlocks()
    {
        var library = BlockLibrary.Shared;
        foreach (var block in library.Catalog.Blocks.OrderBy(b => b.Name))
        {
            var path = library.PathFor(block.Id); var exists = File.Exists(path);
            var thumb = library.ThumbnailPath(block.Id);
            string? error = null;
            if (exists)
            {
                try
                {
                    if (!File.Exists(thumb) || File.GetLastWriteTimeUtc(thumb) < File.GetLastWriteTimeUtc(path))
                    {
                        var editor = new EditorViewModel(); editor.LoadPackage(library.Load(block.Id),path);
                        using var bitmap = editor.RenderPrintPreview(0,false,applyOrientation: false); bitmap.Save(thumb);
                    }
                }
                catch (Exception ex) { error = ex.Message; }
            }
            var content = new StackPanel { Spacing = 7 };
            var imageBox = new Border { Background = Brushes.White,Height = 210,Padding = new Thickness(8) };
            try { if (File.Exists(thumb)) { var bitmap = new Bitmap(thumb); _images.Add(bitmap); imageBox.Child = new Image { Source = bitmap,Stretch = Stretch.Uniform }; } }
            catch (Exception ex) { error = ex.Message; }
            content.Children.Add(imageBox);
            var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            heading.Children.Add(new TextBlock { Text = block.Name,FontWeight = FontWeight.Bold,TextTrimming = TextTrimming.CharacterEllipsis,VerticalAlignment = VerticalAlignment.Center });
            content.Children.Add(heading);
            content.Children.Add(new TextBlock { Text = "Shared custom block",Foreground = Brushes.Turquoise });
            if (!exists || error is not null) content.Children.Add(new TextBlock { Text = exists ? error : "Block file is missing; cached thumbnail retained.",Foreground = Brushes.Orange,TextWrapping = TextWrapping.Wrap });
            async Task OpenBlock(bool unlock)
            {
                if (!File.Exists(path)) { Refresh(); return; }
                if (unlock && !await ConfirmBlockAction("Unlock shared block","Saving changes updates every design using this block. Existing design parameter values will be preserved.\n\nUnlock for editing?","Unlock and edit")) return;
                await _vm.OpenRecentCommand.ExecuteAsync(path);
                if (_vm.Editor.FilePath == path) { _vm.Editor.BlockSourceUnlocked = unlock; Close(); }
            }
            async Task InsertIntoDesign()
            {
                try { _vm.Editor.InsertBlock(block.Id); Close(); }
                catch (Exception ex) { await ConfirmBlockAction("Cannot insert block",ex.Message,"OK"); }
            }
            imageBox.DoubleTapped += async (_,e) => { e.Handled = true; if (_insertBlocks) await InsertIntoDesign(); else await OpenBlock(false); };
            ToolTip.SetTip(imageBox,_insertBlocks ? "Double-click to insert this block into the design" : "Double-click to open the master read-only");
            var open = new MenuItem { Header = "Open read-only",IsEnabled = exists }; open.Click += async (_,_) => await OpenBlock(false);
            var edit = new MenuItem { Header = "Unlock and edit...",IsEnabled = exists }; edit.Click += async (_,_) => await OpenBlock(true);
            var insert = new MenuItem { Header = "Insert into current design",IsEnabled = exists && !_vm.Editor.IsBlockReadOnly && !_vm.Editor.IsEditingBlock };
            insert.Click += async (_,_) => await InsertIntoDesign();
            var delete = new MenuItem { Header = "Delete custom block..." };
            delete.Click += async (_,_) =>
            {
                var references = library.References(block.Id).ToList();
                if (_vm.Editor.ExportPackage().Document.Elements.OfType<Thermalith.Core.Model.BlockElement>().Any(e => e.BlockId == block.Id) && !references.Contains(_vm.Editor.FilePath ?? "",StringComparer.OrdinalIgnoreCase))
                    references.Add("Current design (unsaved block reference)");
                var message = "Delete shared custom block '" + block.Name + "'?";
                if (references.Count > 0) message += "\n\nWARNING: " + references.Count + " known design/block reference(s) will lose this block:\n" + string.Join("\n",references);
                message += "\n\nOther designs may also reference this block. Deleting it breaks those links. Referencing design files will not be deleted.";
                if (await ConfirmBlockAction("Delete shared custom block",message,"Delete block")) { library.Delete(block.Id); Refresh(); }
            };
            var menu = new ContextMenu { ItemsSource = new object[] { insert,open,edit,new Separator(),delete } };
            var more = new Button { Content = new Material.Icons.Avalonia.MaterialIcon { Kind = Material.Icons.MaterialIconKind.DotsVertical },ContextMenu = menu };
            ToolTip.SetTip(more,"Custom block actions"); more.Click += (_,_) => menu.Open(more); Grid.SetColumn(more,1); heading.Children.Add(more);
            if (_showPaths)
            {
                var location = new Button { Content = new TextBlock { Text = path,TextWrapping = TextWrapping.Wrap,FontSize = 11 },Padding = new Thickness(0),Background = Brushes.Transparent,BorderThickness = new Thickness(0) };
                location.Click += (_,_) => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe","/select,\"" + path + "\"") { UseShellExecute = true }); } catch (Exception ex) { _status.Text = ex.Message; } };
                content.Children.Add(location);
            }
            _cards.Children.Add(new Border { Width = 350,Margin = new Thickness(8),Padding = new Thickness(10),BorderBrush = Brushes.Turquoise,BorderThickness = new Thickness(1),CornerRadius = new CornerRadius(5),Child = content });
        }
        _fileState = FileState();
        _status.Text = "Custom blocks: " + library.Catalog.Blocks.Count + ". Folder: " + library.Folder + (_insertBlocks ? ". Double-click to insert; edit inherited values through Fx in your design." : ". Double-click to open read-only; use the menu to insert or unlock.");
    }
}
