using System.Text.Json;
using Thermalith.Core.Model;
using Thermalith.Core.Rendering;
using Thermalith.Core.Serialization;
using Xunit;
namespace Thermalith.Core.Tests;
public class TableBorderTests
{
    private static TableProps Table() => new() { Rows = 2, Cols = 2, BorderWidthMm = 0.1, Cells = [[new TableCell { Content = "A" },new TableCell()], [new TableCell(),new TableCell()]] };
    [Fact]
    public void Presets_update_shared_edges_and_preserve_content()
    {
        var props = TableBorderFormatting.Apply(Table(),0,0,1,1,"none",new CellBorder());
        props = TableBorderFormatting.Apply(props,0,0,0,0,"right",new CellBorder { WidthMm = 0.3, Style = "dashed" });
        Assert.Equal(0.3,props.Cells![0][0].Borders!.Right!.WidthMm);
        Assert.Equal(props.Cells[0][0].Borders!.Right,props.Cells[0][1].Borders!.Left);
        Assert.Equal(0,props.Cells[0][0].Borders!.Top!.WidthMm);
        Assert.Equal("A",props.Cells[0][0].Content);
        var loaded = JsonSerializer.Deserialize<TableProps>(JsonSerializer.Serialize(props,LabelJson.Options),LabelJson.Options)!;
        Assert.Equal("dashed",loaded.Cells![0][1].Borders!.Left!.Style);
    }
    [Fact]
    public void No_borders_overrides_table_defaults_and_prints_no_grid()
    {
        var props = TableBorderFormatting.Apply(Table() with { Cells = null },0,0,1,1,"none",new CellBorder());
        var doc = new LabelDocument { Metadata = new LabelMetadata { Name = "Borders" },Canvas = new Canvas { WidthMm = 30,HeightMm = 20 },Elements = [new TableElement { Id = "t",X = 2,Y = 2,W = 25,H = 15,Props = props }] };
        var raster = new LabelRenderer().Render(doc,new ResolveContext());
        for (var y = 0; y < raster.HeightPx; y++) Assert.All(raster.Row(y).ToArray(),v=>Assert.Equal((byte)0,v));
        var withTop = doc with { Elements = [((TableElement)doc.Elements[0]) with { Props = TableBorderFormatting.Apply(props,0,0,1,1,"top",new CellBorder { WidthMm = 0.4 }) }] };
        var topRaster = new LabelRenderer().Render(withTop,new ResolveContext());
        Assert.Contains(Enumerable.Range(0,topRaster.HeightPx),y=>topRaster.Row(y).ToArray().Any(v=>v!=0));
    }
    [Fact]
    public void Inside_preset_does_not_draw_through_merged_cells()
    {
        var props = new TableProps { Rows = 1,Cols = 2,BorderWidthMm = 0,Cells = [[new TableCell { ColSpan = 2 },new TableCell()]] };
        var updated = TableBorderFormatting.Apply(props,0,0,0,1,"inner",new CellBorder());
        Assert.Null(updated.Cells![0][0].Borders);
    }
}
