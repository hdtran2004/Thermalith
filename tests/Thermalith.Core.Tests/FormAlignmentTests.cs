using Thermalith.Core.Model;
using Thermalith.Core.Rendering;
using Xunit;

namespace Thermalith.Core.Tests;

public class FormAlignmentTests
{
    [Theory]
    [InlineData(40, 1.1, 37.8)]
    [InlineData(20, 6.4, 5)]
    [InlineData(40, 0.7, 38.6)]
    [InlineData(20, -2, 25.5)]
    public void Centering_preserves_fractional_mm_and_is_idempotent(double label, double start, double size)
    {
        var offset = FormAlignment.CenterOffset(label, start, size);
        var moved = start + offset;
        Assert.Equal(label / 2, moved + size / 2, 10);
        Assert.Equal(0, FormAlignment.CenterOffset(label, moved, size), 10);
    }

    [Fact]
    public void Rotated_text_is_centered_using_current_rendered_bounds()
    {
        var renderer = new LabelRenderer();
        var text = new TextElement
        {
            Id = "text", X = 2.3, Y = 3.1, W = 12, H = 5, Rotation = 30,
            Props = new TextProps { Content = "Longer edited text", FontSizePt = 11, Wrap = "none", AutoSize = false },
        };
        var doc = new LabelDocument
        {
            Metadata = new LabelMetadata { Name = "alignment" },
            Canvas = new Canvas { WidthMm = 40, HeightMm = 20, Dpi = 300 },
            Elements = [text],
        };
        RenderedRect Measure(LabelDocument d, TextElement e)
        {
            var bounds = renderer.MeasureRenderedBounds(renderer.Resolve(d, new ResolveContext()))[e.Id];
            return LabelRenderer.RotatedAabb(bounds, e.Rotation, e.X + e.W / 2, e.Y + e.H / 2);
        }
        var b = Measure(doc, text);
        var moved = text with
        {
            X = text.X + FormAlignment.CenterOffset(40, b.XMm, b.WMm),
            Y = text.Y + FormAlignment.CenterOffset(20, b.YMm, b.HMm),
        };
        var actual = Measure(doc with { Elements = [moved] }, moved);
        Assert.Equal(20, actual.XMm + actual.WMm / 2, 4);
        Assert.Equal(10, actual.YMm + actual.HMm / 2, 4);
    }
}
