using Thermalith.App.ViewModels;
using Thermalith.Core.Model;
using Thermalith.Core.Serialization;
using Xunit;
[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Thermalith.App.Tests;
public class OverlappingTableTests
{
    private static LabelPackage Package() => new()
    {
        Manifest = new Manifest { Id = "overlap-test", Name = "Overlap test" },
        Document = new LabelDocument { Metadata = new LabelMetadata { Name = "Overlap test" }, Canvas = new Canvas { WidthMm = 50, HeightMm = 40, Dpi = 203 },
            Elements = [
                new TableElement { Id = "large", X = 1, Y = 1, W = 40, H = 30, Props = new TableProps { Rows = 5, Cols = 4 } },
                new TableElement { Id = "small", X = 1, Y = 1, W = 10, H = 10, Props = new TableProps { Rows = 1, Cols = 1 } }
            ] }
    };
    [Fact]
    public void Switching_to_smaller_overlapping_table_resets_cell_before_redraw_and_edits_correct_table()
    {
        var editor = new EditorViewModel();
        editor.LoadPackage(Package(), "overlap-test.nlbl");
        editor.Zoom = 1;
        var scale = 203 / 25.4;
        Assert.True(editor.TryEnterCellMode(36 * scale, 27 * scale));
        Assert.Equal((4, 3, 4, 3), editor.CellBlock);
        editor.ExitCellMode();
        Assert.True(editor.TryEnterCellMode(5 * scale, 5 * scale));
        Assert.Equal("small", editor.CellEditTableId);
        Assert.Equal((0, 0, 0, 0), editor.CellBlock);
        editor.BeginCellEdit();
        editor.CellEditText = "second table";
        editor.CommitCellEdit();
        editor.BeginCellEdit();
        Assert.Equal("second table", editor.CellEditText);
        editor.CancelCellEdit();
    }
    [Fact]
    public void Clicking_overlapping_front_table_exits_old_cell_mode_and_loading_clears_it()
    {
        var editor = new EditorViewModel();
        editor.LoadPackage(Package(), "overlap-test.nlbl");
        editor.Zoom = 1;
        var scale = 203 / 25.4;
        Assert.True(editor.TryEnterCellMode(36 * scale, 27 * scale));
        Assert.False(editor.CellPointerDown(5 * scale, 5 * scale));
        Assert.False(editor.InCellMode);
        Assert.True(editor.TryEnterCellMode(5 * scale, 5 * scale));
        editor.BeginCellEdit();
        editor.NewDocument();
        Assert.False(editor.InCellMode);
        Assert.False(editor.IsCellEditing);
        Assert.Empty(editor.CellHighlights);
    }
    [Fact]
    public void Borderless_table_guides_are_visible_in_editor_but_not_printed()
    {
        var package = Package();
        var table = (TableElement)package.Document.Elements[0];
        var doc = package.Document with { Elements = [table with { Props = table.Props with { BorderWidthMm = 0 } }] };
        var editor = new EditorViewModel();
        editor.LoadPackage(package with { Document = doc }, "borderless.nlbl");
        editor.SelectAt(2 * 203 / 25.4 * editor.Zoom, 2 * 203 / 25.4 * editor.Zoom, false);
        Assert.Equal(20, editor.TableGuides.Count);
        var raster = editor.RenderForPrint();
        for (var y = 0; y < raster.HeightPx; y++) Assert.All(raster.Row(y).ToArray(), pixel => Assert.Equal((byte)0, pixel));
        editor.Zoom = 2;
        Assert.Equal(20, editor.TableGuides.Count);
    }    [Fact]
    public void Workspace_print_view_preserves_design_preferences_and_closes_inline_editors()
    {
        var editor = new EditorViewModel();
        editor.LoadPackage(Package(), "overlap-test.nlbl");
        editor.ShowGrid = true;
        editor.ShowSafeArea = true;
        editor.ShowParameterReferences = true;
        editor.Zoom = 1;
        Assert.True(editor.TryEnterCellMode(5 * 203 / 25.4, 5 * 203 / 25.4));
        editor.BeginCellEdit();
        editor.IsPrintView = true;
        Assert.False(editor.IsCellEditing);
        Assert.False(editor.InCellMode);
        Assert.False(editor.ShowWorkspaceGuides);
        Assert.True(editor.ShowGrid);
        Assert.True(editor.ShowParameterReferences);
        Assert.False(editor.Dirty);
        editor.IsPrintView = false;
        Assert.True(editor.ShowWorkspaceGuides);
        Assert.True(editor.ShowGrid);
        Assert.True(editor.ShowParameterReferences);
    }}
