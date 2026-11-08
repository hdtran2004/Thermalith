using Thermalith.App.ViewModels;
using Thermalith.App.ViewModels.Editors;
using Thermalith.Core.Catalog;
using Thermalith.Core.Model;
using Xunit;
namespace Thermalith.App.Tests;
public class StockSetupTests
{
    [Fact] public void Stock_selection_does_not_rename_design()
    {
        var stock = new RollDefinition { Name = "Silver", Barcode = "sku", WidthMm = 30, HeightMm = 20, PaperType = "gap" };
        var vm = new RollDialogViewModel(new RollDefinition(), "New") { DesignName = "Wiring" };
        vm.LoadStocks(new[] { stock }); vm.SelectedStockIndex = 1;
        Assert.Equal("Wiring", vm.DesignName); Assert.Equal("sku", vm.ToRoll().Barcode);
        Assert.Equal(30, vm.Width); vm.SelectedStockIndex = 0;
        Assert.Empty(vm.Name); Assert.Null(vm.ToRoll().Barcode);
    }
    [Fact] public void Layout_swaps_dimensions_and_feed_rotation_once()
    {
        var doc = new LabelDocument { Metadata = new LabelMetadata { Name = "Test" }, Canvas = new Canvas { WidthMm = 80, HeightMm = 50 } };
        var changes = 0; var vm = new CanvasEditorViewModel(doc, () => changes++);
        vm.Layout = "Portrait";
        var result = vm.Apply(doc);
        Assert.Equal(50, result.Canvas.WidthMm); Assert.Equal(80, result.Canvas.HeightMm);
        Assert.Equal(90, result.Canvas.OrientationDeg); Assert.Equal(1, changes);
    }
    [Theory]
    [InlineData(90, 50, 80)]
    [InlineData(270, 50, 80)]
    [InlineData(180, 80, 50)]
    public void Rotation_updates_workspace_dimensions(int rotation, double width, double height)
    {
        var doc = new LabelDocument { Metadata = new LabelMetadata { Name = "Test" }, Canvas = new Canvas { WidthMm = 80, HeightMm = 50 } };
        var changes = 0; var vm = new CanvasEditorViewModel(doc, () => changes++);
        vm.OrientationDeg = rotation;
        Assert.Equal(width, vm.WidthMm); Assert.Equal(height, vm.HeightMm);
        Assert.Equal(rotation, vm.Apply(doc).Canvas.OrientationDeg); Assert.Equal(1, changes);
        vm.OrientationDeg = 0;
        Assert.Equal(80, vm.WidthMm); Assert.Equal(50, vm.HeightMm);
    }
    [Fact] public void Editing_stock_updates_shape_and_corners_without_renaming_artwork()
    {
        var doc = new LabelDocument { Metadata = new LabelMetadata { Name = "Product label" }, Canvas = new Canvas { WidthMm = 50, HeightMm = 30, StockName = "Silver" } };
        var changes = 0; var vm = new CanvasEditorViewModel(doc, () => changes++);
        vm.UpdateStock(new RollDefinition { Name = "Silver", WidthMm = 50, HeightMm = 30, Shape = "rounded", CornerRadiusMm = 2 });
        var updated = vm.Apply(doc);
        Assert.Equal("Product label",updated.Metadata.Name); Assert.Equal("rounded",updated.Canvas.Shape);
        Assert.Equal(2,updated.Canvas.CornerRadiusMm); Assert.Equal("rounded",updated.Canvas.Stock!.Shape);
        Assert.Equal(1,changes);
    }
}

