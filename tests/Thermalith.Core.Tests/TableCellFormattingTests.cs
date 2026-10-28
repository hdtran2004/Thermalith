using Thermalith.Core.Model;
using Xunit;

namespace Thermalith.Core.Tests;

public class TableCellFormattingTests
{
    [Fact]
    public void Copy_changes_appearance_but_preserves_target_content_and_merges()
    {
        var source = new TableCell { Content = "source", FontFamily = "Arial", FontSizePt = 15,
            Bold = true, Italic = true, Justify = new Justify { H = "right", V = "bottom" }, Fill = 75, TextColor = "white" };
        var target = new TableCell { Content = "keep me", RowSpan = 2, ColSpan = 3 };
        var result = TableCellFormatting.Apply(source, target);
        Assert.Equal("keep me", result.Content);
        Assert.Equal(2, result.RowSpan);
        Assert.Equal(3, result.ColSpan);
        Assert.Equal("Arial", result.FontFamily);
        Assert.Equal(15, result.FontSizePt);
        Assert.True(result.Bold);
        Assert.True(result.Italic);
        Assert.Equal(source.Justify, result.Justify);
        Assert.Equal(75, result.Fill);
        Assert.Equal("white", result.TextColor);
        Assert.Equal(new TableCell().FontFamily, TableCellFormatting.Apply(new TableCell(), result).FontFamily);
        Assert.Equal("keep me", target.Content);
    }
}
