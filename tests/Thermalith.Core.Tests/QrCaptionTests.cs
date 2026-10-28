using Thermalith.Core.Model;
using Thermalith.Core.Rendering;
using Thermalith.Core.Serialization;
using Xunit;

namespace Thermalith.Core.Tests;

public class QrCaptionTests
{
    private static LabelDocument Doc(QrProps props) => new()
    {
        Metadata = new LabelMetadata { Name = "QR caption" },
        Canvas = new Canvas { WidthMm = 40, HeightMm = 40, Dpi = 300 },
        Elements = [new QrElement { Id = "qr", X = 2, Y = 2, W = 30, H = 30, Props = props }],
    };

    [Fact]
    public void Caption_settings_round_trip_and_resolve_to_the_renderer()
    {
        var props = new QrProps { Value = "QR-123", ShowText = true, TextPosition = "above",
            TextFontFamily = "Roboto", TextSizePt = 11, TextBold = true, TextItalic = true,
            TextAlignment = "right", TextWrap = "none", TextLetterSpacing = -0.2,
            TextHeightMm = 7, TextGapMm = 2 };
        var json = System.Text.Json.JsonSerializer.Serialize(Doc(props), LabelJson.Options);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<LabelDocument>(json, LabelJson.Options)!;
        var qr = Assert.IsType<ResolvedQr>(Assert.Single(new LabelRenderer().Resolve(loaded, new ResolveContext()).Elements));
        Assert.True(qr.ShowText);
        Assert.Equal(props.Value, qr.Value);
        Assert.Equal(props.TextPosition, qr.TextPosition);
        Assert.Equal(props.TextSizePt, qr.TextSizePt);
        Assert.Equal(props.TextFontFamily, qr.TextFontFamily);
        Assert.True(qr.TextBold && qr.TextItalic);
        Assert.Equal(props.TextAlignment, qr.TextAlignment);
        Assert.Equal(props.TextWrap, qr.TextWrap);
        Assert.Equal(props.TextLetterSpacing, qr.TextLetterSpacing);
        Assert.Equal(props.TextHeightMm, qr.TextHeightMm);
        Assert.Equal(props.TextGapMm, qr.TextGapMm);
    }

    [Theory]
    [InlineData("above", "left")]
    [InlineData("below", "left")]
    [InlineData("below", "right")]
    [InlineData("below", "center")]
    public void Caption_overflows_and_anchors_to_actual_code_without_resizing_it(string position, string alignment)
    {
        var renderer = new LabelRenderer();
        var props = new QrProps { Value = "LONG QR VALUE 123456789", ShowText = true,
            TextPosition = position, TextAlignment = alignment, TextWrap = "none",
            TextSizePt = 11, TextHeightMm = 4, TextGapMm = 1 };
        var doc = Doc(props) with
        {
            Canvas = new Canvas { WidthMm = 100, HeightMm = 80, Dpi = 300 },
            Elements = [new QrElement { Id = "qr", X = 35, Y = 25, W = 30, H = 16, Props = props }],
        };
        var withCaption = renderer.Render(doc, new ResolveContext());
        var withoutCaption = renderer.Render(doc with { Elements = [((QrElement)doc.Elements[0]) with { Props = props with { ShowText = false } }] }, new ResolveContext());
        var scale = 300 / 25.4;
        // The QR's entire original box is identical: no shrink, repositioning, or text intrusion.
        for (var y = (int)Math.Ceiling(25 * scale); y < (int)Math.Floor(41 * scale); y++)
            Assert.Equal(withoutCaption.Row(y).ToArray(), withCaption.Row(y).ToArray());
        var resolved = renderer.Resolve(doc, new ResolveContext());
        var bounds = renderer.MeasureRenderedBounds(resolved)["qr"];
        Assert.True(bounds.WMm > 30); // long fixed-size value is not clipped/wrapped to QR width
        Assert.True(position == "above" ? bounds.YMm < 25 : bounds.YMm + bounds.HMm > 41);
        (int left, int right) Ink(int first, int last, Niimbot.Net.Encoding.MonochromeBitmap bitmap)
        {
            int left = bitmap.WidthPx, right = -1;
            for (var y = first; y < last; y++)
                for (var x = 0; x < bitmap.WidthPx; x++)
                    if ((bitmap.Row(y)[x / 8] & (0x80 >> (x % 8))) != 0) { left = Math.Min(left, x); right = Math.Max(right, x); }
            return (left, right);
        }
        var code = Ink((int)Math.Ceiling(25 * scale), (int)Math.Floor(41 * scale), withoutCaption);
        var text = position == "above" ? Ink(0, (int)(25 * scale), withCaption)
            : Ink((int)Math.Ceiling(41 * scale), withCaption.HeightPx, withCaption);
        Assert.True(text.right > text.left);
        var error = alignment switch
        {
            "left" => Math.Abs(text.left - code.left),
            "right" => Math.Abs(text.right - code.right),
            _ => Math.Abs((text.left + text.right - code.left - code.right) / 2.0),
        };
        Assert.InRange(error, 0, 4); // font bearings/threshold allow a few device pixels
    }

    [Fact]
    public void Legacy_qr_caption_is_disabled_by_default()
    {
        var renderer = new LabelRenderer();
        var old = renderer.Render(Doc(new QrProps { Value = "QR-123" }), new ResolveContext());
        var explicitOff = renderer.Render(Doc(new QrProps { Value = "QR-123", ShowText = false, TextHeightMm = 10 }), new ResolveContext());
        for (var y = 0; y < old.HeightPx; y++)
            Assert.Equal(old.Row(y).ToArray(), explicitOff.Row(y).ToArray());
    }
}
