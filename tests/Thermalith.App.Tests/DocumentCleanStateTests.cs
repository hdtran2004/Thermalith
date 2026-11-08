using Thermalith.App.ViewModels;
using Thermalith.App.ViewModels.Editors;
using Thermalith.Core.Catalog;
using Thermalith.Core.Model;
using Thermalith.Core.Serialization;
using Xunit;
namespace Thermalith.App.Tests;
public class DocumentCleanStateTests
{
    [Fact] public void New_stock_setup_is_clean_but_later_user_edits_are_dirty()
    {
        var editor = new EditorViewModel(); editor.NewDocument("Panel");
        var stock = new RollDefinition { Name = "Silver", WidthMm = 50, HeightMm = 30, Shape = "rounded", CornerRadiusMm = 2 };
        editor.ApplyRoll(50,30,"rounded",300,48,2,stock.Name); editor.SetDesignStock(stock);
        editor.RotateLeft(); editor.CompleteNewDocumentSetup();
        Assert.False(editor.Dirty);
        var inspector = Assert.IsType<CanvasEditorViewModel>(editor.InspectorTarget);
        Assert.Equal("Silver",inspector.StockName); Assert.Contains("Silver",inspector.StockChoices);
        inspector.Name = "Edited"; Assert.True(editor.Dirty);
    }
    [Fact] public void Opening_and_targeting_printer_does_not_dirty_existing_artwork()
    {
        var editor = new EditorViewModel();
        editor.LoadPackage(new LabelPackage { Manifest = new Manifest { Id = "clean-test" }, Document = new LabelDocument {
            Metadata = new LabelMetadata { Name = "Saved" }, Canvas = new Canvas { WidthMm = 50, HeightMm = 30, StockName = "Legacy stock" },
            Elements = [new TextElement { Id = "text", X = 1,Y = 1,W = 20,H = 10 }] } }, "saved.nlbl");
        Assert.False(editor.Dirty); editor.SetPrinterTarget(48,300); Assert.False(editor.Dirty);
        var inspector = Assert.IsType<CanvasEditorViewModel>(editor.InspectorTarget);
        Assert.Contains("Legacy stock",inspector.StockChoices);
        inspector.Name = "Changed"; Assert.True(editor.Dirty);
        editor.SetPrinterTarget(48,300); Assert.True(editor.Dirty);
    }
}
