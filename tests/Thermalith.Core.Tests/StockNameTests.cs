using System.Text.Json;
using Thermalith.Core.Model;
using Thermalith.Core.Serialization;
using Xunit;
namespace Thermalith.Core.Tests;
public class StockNameTests
{
    [Fact]
    public void Design_and_stock_names_are_saved_independently()
    {
        var doc = new LabelDocument { Metadata = new LabelMetadata { Name = "Product identification" }, Canvas = new Canvas { WidthMm = 80, HeightMm = 50, StockName = "Transparent 80W x 50H" } };
        var saved = JsonSerializer.Serialize(doc, LabelJson.Options);
        var loaded = JsonSerializer.Deserialize<LabelDocument>(saved, LabelJson.Options)!;
        Assert.Equal("Product identification", loaded.Metadata.Name);
        Assert.Equal("Transparent 80W x 50H", loaded.Canvas.StockName);
        var renamed = loaded with { Metadata = loaded.Metadata with { Name = "Another design" } };
        Assert.Equal(loaded.Canvas.StockName, renamed.Canvas.StockName);
    }
    [Fact]
    public void Older_files_without_stock_names_keep_their_design_name()
    {
        var doc = new LabelDocument { Metadata = new LabelMetadata { Name = "Existing design" }, Canvas = new Canvas { WidthMm = 50, HeightMm = 30 } };
        var loaded = JsonSerializer.Deserialize<LabelDocument>(JsonSerializer.Serialize(doc, LabelJson.Options), LabelJson.Options)!;
        Assert.Equal("Existing design", loaded.Metadata.Name);
        Assert.Null(loaded.Canvas.StockName);
    }
}
