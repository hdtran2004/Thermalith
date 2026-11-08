using Thermalith.Core.Model;
using Thermalith.Core.Rendering;
using Xunit;
namespace Thermalith.Core.Tests;
public class PreviewLayoutTests
{
    [Theory]
    [InlineData(0.05)]
    [InlineData(-0.2)]
    public void Smoothing_preserves_line_breaks_near_the_wrap_boundary(double widthAdjustment)
    {
        var renderer = new LabelRenderer();
        var props = new TextProps { Content = "FP-280 FIRE ALARM JUNCTION BOX (JB1)", FontFamily = "Roboto", FontSizePt = 12, Bold = true, Wrap = "word" };
        var width = renderer.MeasureTextMm(props, 203).WidthMm + widthAdjustment;
        var doc = new LabelDocument { Metadata = new LabelMetadata { Name = "Wrap boundary" }, Canvas = new Canvas { WidthMm = width + 4, HeightMm = 40, Dpi = 203 },
            Elements = [new TextElement { Id = "heading", X = 2, Y = 2, W = width, H = 30, Props = props }] };
        var basePreview = renderer.RenderGray(doc, new ResolveContext());
        var smooth = renderer.RenderGray(doc, new ResolveContext(), new RenderOptions { PreviewRasterScale = 6 });
        Assert.Equal(basePreview.WidthPx * 6, smooth.WidthPx);
        Assert.Equal(basePreview.HeightPx * 6, smooth.HeightPx);
        Assert.Equal(TextBands(basePreview), TextBands(smooth));
        var normalPrint = renderer.Render(doc, new ResolveContext());
        var printWithPreviewOption = renderer.Render(doc, new ResolveContext(), new RenderOptions { PreviewRasterScale = 6 });
        Assert.Equal(normalPrint.WidthPx, printWithPreviewOption.WidthPx);
        for (var y = 0; y < normalPrint.HeightPx; y++) Assert.True(normalPrint.Row(y).SequenceEqual(printWithPreviewOption.Row(y)));
    }
    private static int TextBands(GrayBitmap bitmap)
    {
        var bands = 0; var previous = false;
        for (var y = 0; y < bitmap.HeightPx; y++)
        {
            var ink = bitmap.Gray.AsSpan(y * bitmap.WidthPx, bitmap.WidthPx).ContainsAnyInRange((byte)0, (byte)127);
            if (ink && !previous) bands++;
            previous = ink;
        }
        return bands;
    }
}
