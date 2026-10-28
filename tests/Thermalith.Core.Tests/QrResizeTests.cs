using Thermalith.Core.Rendering;
using Xunit;
namespace Thermalith.Core.Tests;
public class QrResizeTests
{
    [Theory]
    [InlineData(0, -1, -1)]
    [InlineData(0, 1, 1)]
    [InlineData(30, -1, 1)]
    [InlineData(90, 1, 0)]
    [InlineData(45, 0, -1)]
    public void Square_resize_keeps_opposite_handle_anchored(double angle, int hx, int hy)
    {
        var original = new RenderedRect(3, 5, 20, 14);
        var resized = QrResize.Square(original, 26, 25, hx, hy, angle);
        Assert.Equal(resized.WMm, resized.HMm);
        (double x, double y) Anchor(RenderedRect r)
        {
            var rad = angle * Math.PI / 180;
            var x = -hx * r.WMm / 2;
            var y = -hy * r.HMm / 2;
            return (r.XMm + r.WMm / 2 + x * Math.Cos(rad) - y * Math.Sin(rad),
                r.YMm + r.HMm / 2 + x * Math.Sin(rad) + y * Math.Cos(rad));
        }
        var before = Anchor(original); var after = Anchor(resized);
        Assert.Equal(before.x, after.x, 8);
        Assert.Equal(before.y, after.y, 8);
    }
}
