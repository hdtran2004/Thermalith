using System.Text.Json;
using Thermalith.Core.Model;
using Thermalith.Core.Rendering;
using Thermalith.Core.Serialization;
using Xunit;
namespace Thermalith.Core.Tests;
public class CustomBlockTests
{
    private static LabelPackage Source(string text = "{Serial}") => new() { Manifest = new Manifest { Id = "block" },Document = new LabelDocument {
        CustomBlockId = "block",Metadata = new LabelMetadata { Name = "Header" },Canvas = new Canvas { WidthMm = 40,HeightMm = 20,OrientationDeg = 90 },
        Parameters = [new LabelParameter { Name = "Serial",Prefix = "DEFAULT" }],Elements = [new TextElement { Id = "text",X = 1,Y = 2,W = 25,H = 6,Props = new TextProps { Content = text } }] } };
    private static LabelDocument Host() => new() { Metadata = new LabelMetadata { Name = "Host" },Canvas = new Canvas { WidthMm = 80,HeightMm = 50 },
        Parameters = [new LabelParameter { Name = "Serial",Prefix = "DESIGN" }],Elements = [new BlockElement { Id = "ref",BlockId = "block",X = 3,Y = 4,W = 40,H = 20,ParameterNames = ["Serial"] }] };
    [Fact] public void Host_canvas_and_parameter_values_win_over_source_stock_and_defaults()
    {
        var result = LabelResolver.Resolve(Host(),new ResolveContext { BlockProvider = _ => Source() });
        Assert.Equal(80,result.Canvas.WidthMm); Assert.Equal(50,result.Canvas.HeightMm);
        var block = Assert.IsType<ResolvedBlock>(Assert.Single(result.Elements));
        Assert.Equal(3,block.XMm); Assert.Equal(4,block.YMm);
        Assert.Equal("DESIGN",Assert.IsType<ResolvedText>(Assert.Single(block.Elements)).Text);
    }
    [Fact] public void Reference_roundtrips_and_source_changes_are_live()
    {
        var host = JsonSerializer.Deserialize<LabelDocument>(JsonSerializer.Serialize(Host(),LabelJson.Options),LabelJson.Options)!;
        var source = Source("First"); var ctx = new ResolveContext { BlockProvider = _ => source };
        Assert.Equal("First",Assert.IsType<ResolvedText>(Assert.IsType<ResolvedBlock>(LabelResolver.Resolve(host,ctx).Elements[0]).Elements[0]).Text);
        source = Source("Updated");
        Assert.Equal("Updated",Assert.IsType<ResolvedText>(Assert.IsType<ResolvedBlock>(LabelResolver.Resolve(host,ctx).Elements[0]).Elements[0]).Text);
        Assert.Equal("DESIGN",host.Parameters![0].Prefix);
    }
    [Fact] public void Missing_and_circular_blocks_fail_explicitly()
    {
        Assert.Throws<InvalidOperationException>(() => LabelResolver.Resolve(Host(),new ResolveContext()));
        var recursive = Source() with { Document = Source().Document with { Elements = [new BlockElement { Id = "loop",BlockId = "block",W = 40,H = 20 }] } };
        Assert.Throws<InvalidOperationException>(() => LabelResolver.Resolve(Host(),new ResolveContext { BlockProvider = _ => recursive }));
    }
    [Fact] public void Light_blue_inherited_text_is_an_editor_only_cue()
    {
        var renderer = new LabelRenderer(); var resolved = renderer.Resolve(Host(),new ResolveContext { BlockProvider = _ => Source("Text") });
        using var preview = SkiaSharp.SKBitmap.Decode(renderer.RenderDesignPreview(resolved));
        var blue = false;
        for (var y = 0; y < preview.Height; y++) for (var x = 0; x < preview.Width; x++)
        { var color = preview.GetPixel(x,y); if (color.Blue > color.Red + 30 && color.Green > color.Red + 20) blue = true; }
        Assert.True(blue);
        var print = renderer.Render(resolved); Assert.Contains(print.Packed, b => b != 0);
        var gray = renderer.RenderGray(resolved); Assert.Equal(print.WidthPx,gray.WidthPx);
    }
}
