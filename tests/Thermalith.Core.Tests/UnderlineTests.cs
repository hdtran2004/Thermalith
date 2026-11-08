using System.Text.Json;
using Thermalith.Core.Model;
using Thermalith.Core.Rendering;
using Thermalith.Core.Serialization;
using Xunit;
namespace Thermalith.Core.Tests;
public class UnderlineTests
{
    private static LabelDocument Document(bool underline) => new()
    {
        Metadata = new LabelMetadata { Name = "Underline" },
        Canvas = new Canvas { WidthMm = 60, HeightMm = 40, Dpi = 300 },
        Elements = [
            new QrElement { Id = "qr", X = 2, Y = 2, W = 15, H = 15, Props = new QrProps { Value = "TEST", ShowText = true, TextUnderline = underline } },
            new TableElement { Id = "table", X = 25, Y = 2, W = 30, H = 15, Props = new TableProps { Cols = 1, Rows = 1, Cells = [[new TableCell { Content = "TEST", Underline = underline }]] } }
        ]
    };
    [Fact]
    public void QR_and_table_underline_survive_save_and_resolve()
    {
        var loaded = JsonSerializer.Deserialize<LabelDocument>(JsonSerializer.Serialize(Document(true), LabelJson.Options), LabelJson.Options)!;
        var result = LabelResolver.Resolve(loaded, new ResolveContext());
        Assert.True(Assert.IsType<ResolvedQr>(result.Elements[0]).TextUnderline);
        Assert.True(Assert.IsType<ResolvedTable>(result.Elements[1]).Cells[0][0].Underline);
        var copied = TableCellFormatting.Apply(new TableCell { Underline = true }, new TableCell { Content = "Keep content" });
        Assert.True(copied.Underline);
        Assert.Equal("Keep content", copied.Content);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Underline_changes_print_raster(int index)
    {
        var renderer = new LabelRenderer();
        var a = Document(false); a = a with { Elements = [a.Elements[index]] };
        var b = Document(true); b = b with { Elements = [b.Elements[index]] };
        var normal = renderer.Render(a, new ResolveContext());
        var underlined = renderer.Render(b, new ResolveContext());
        Assert.Contains(Enumerable.Range(0, normal.HeightPx), y => !normal.Row(y).SequenceEqual(underlined.Row(y)));
    }
}
