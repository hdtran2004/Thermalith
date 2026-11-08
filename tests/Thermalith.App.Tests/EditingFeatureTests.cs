using Thermalith.App.ViewModels;
using Thermalith.Core.Model;
using Thermalith.Core.Serialization;
using Xunit;
namespace Thermalith.App.Tests;
public class EditingFeatureTests
{
    [Fact]
    public void Whole_table_formatting_updates_every_cell_and_preserves_content()
    {
        var doc = new LabelDocument { Metadata = new LabelMetadata { Name = "Table" },Canvas = new Canvas { WidthMm = 50,HeightMm = 30,Dpi = 203 }, Elements = [new TableElement { Id = "table",X = 1,Y = 1,W = 40,H = 20,Props = new TableProps { Rows = 2,Cols = 2,Cells = [[new TableCell { Content = "Keep" },new TableCell()],[new TableCell(),new TableCell()]] } }] };
        var editor = new EditorViewModel(); editor.LoadPackage(new LabelPackage { Manifest = new Manifest { Id = "format-test" },Document = doc },"format-test.nlbl"); editor.Zoom = 1;
        editor.SelectAt(5*203/25.4,5*203/25.4,false);
        editor.CellFontFamily = "Arial"; editor.CellFontSizePt = 14; editor.CellBold = true; editor.CellUnderline = true; editor.CellAlignH = "right";
        Assert.True(editor.TryEnterCellMode(35*203/25.4,17*203/25.4));
        Assert.Equal("Arial",editor.CellFontFamily); Assert.Equal(14,editor.CellFontSizePt); Assert.True(editor.CellBold); Assert.True(editor.CellUnderline); Assert.Equal("right",editor.CellAlignH);
        editor.ExitCellMode(); Assert.True(editor.TryEnterCellMode(5*203/25.4,5*203/25.4)); editor.BeginCellEdit(); Assert.Equal("Keep",editor.CellEditText); editor.CancelCellEdit();
    }
    [Fact]
    public void Group_drag_snaps_as_one_unit_and_clears_guides_after_release()
    {
        var doc = new LabelDocument { Metadata = new LabelMetadata { Name = "Snap" },Canvas = new Canvas { WidthMm = 80,HeightMm = 50,Dpi = 203 }, Elements = [
            new ShapeElement { Id = "a",X = 1,Y = 1,W = 10,H = 10 },new ShapeElement { Id = "b",X = 15,Y = 1,W = 10,H = 10 },new ShapeElement { Id = "target",X = 35,Y = 20,W = 10,H = 10 }
        ] };
        var editor = new EditorViewModel(); editor.LoadPackage(new LabelPackage { Manifest = new Manifest { Id = "snap-test" },Document = doc },"snap-test.nlbl"); editor.Zoom = 1; var s=203/25.4;
        editor.SelectAt(3*s,3*s,false); editor.SelectAt(17*s,3*s,true); editor.BeginDrag(DragMode.Move,Handle.TopLeft); editor.DragTo(33.8*s,19*s);
        Assert.Equal(49,editor.SelectedEditor!.X,5); Assert.NotEmpty(editor.SmartGuides); editor.EndDrag(); Assert.Empty(editor.SmartGuides);
    }
}
