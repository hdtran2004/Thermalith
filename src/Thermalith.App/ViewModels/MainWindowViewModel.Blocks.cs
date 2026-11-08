using CommunityToolkit.Mvvm.Input;
using Thermalith.App.Services;
namespace Thermalith.App.ViewModels;
public partial class MainWindowViewModel
{
    [RelayCommand]
    private async Task SaveAsCustomBlockAsync()
    {
        if (Editor.IsEditingBlock) { if (Dialogs is not null) await Dialogs.MessageAsync("Custom block","Finish the current block edit first."); return; }
        try
        {
            var package = Editor.ExportPackage();
            var record = BlockLibrary.Shared.Create(package with { Document = package.Document with { Parameters = Editor.Parameters.ToList() } });
            Editor.LoadPackage(BlockLibrary.Shared.Load(record.Id),BlockLibrary.Shared.PathFor(record.Id));
            Editor.BlockSourceUnlocked = true;
            StatusMessage = "Created custom block: " + record.Name;
        }
        catch (Exception ex) { if (Dialogs is not null) await Dialogs.MessageAsync("Save custom block",ex.Message); }
    }
    [RelayCommand]
    private async Task UnlockCustomBlockAsync()
    {
        if (Dialogs is null || Editor.CustomBlockId is not { } id) return;
        var count = BlockLibrary.Shared.References(id).Count;
        if (await Dialogs.ConfirmAsync("Unlock shared custom block",$"Changes saved here update every design using this block. Known references: {count}. Design parameter values will be preserved.\n\nUnlock for editing?","Unlock and edit"))
            Editor.BlockSourceUnlocked = true;
    }
    [RelayCommand]
    private async Task EditBlockInPlaceAsync()
    {
        if (Dialogs is null || !Editor.HasSelectedBlock) return;
        if (Printer.IsPrinting) { await Dialogs.MessageAsync("Edit custom block","Finish printing before editing a shared block."); return; }
        if (!await Dialogs.ConfirmAsync("Unlock shared block in place","You are editing the shared block, not a private copy. Saving updates all designs referencing it. Their parameter values and label sizes are preserved.\n\nUnlock this block for editing?","Unlock and edit")) return;
        try { Editor.BeginBlockEdit(); StatusMessage = "Editing shared block in place. Save block changes or Cancel when finished."; }
        catch (Exception ex) { await Dialogs.MessageAsync("Edit custom block",ex.Message); }
    }
    [RelayCommand]
    private async Task SaveBlockChangesAsync()
    {
        try { Editor.SaveBlockEdit(); StatusMessage = "Shared block updated."; }
        catch (Exception ex) { if (Dialogs is not null) await Dialogs.MessageAsync("Save block changes",ex.Message); }
    }
    [RelayCommand]
    private async Task CancelBlockChangesAsync()
    {
        if (!Editor.IsEditingBlock) return;
        if (Editor.Dirty && (Dialogs is null || !await Dialogs.ConfirmAsync("Discard block edits","Discard the unsaved changes to this block?","Discard"))) return;
        Editor.CancelBlockEdit(); StatusMessage = "Block editing cancelled.";
    }
}
