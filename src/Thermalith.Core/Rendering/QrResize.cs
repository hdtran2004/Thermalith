namespace Thermalith.Core.Rendering;

public static class QrResize
{
    /// <summary>Square resize preserving the opposite handle in the element's rotated frame.</summary>
    public static RenderedRect Square(RenderedRect original, double width, double height, int hx, int hy, double rotation)
    {
        var side = hx == 0 ? height : hy == 0 ? width
            : Math.Abs(width - original.WMm) >= Math.Abs(height - original.HMm) ? width : height;
        side = Math.Max(0.1, side);
        var radians = rotation * Math.PI / 180;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var dx = hx * (side - original.WMm) / 2;
        var dy = hy * (side - original.HMm) / 2;
        var cx = original.XMm + original.WMm / 2 + dx * cos - dy * sin;
        var cy = original.YMm + original.HMm / 2 + dx * sin + dy * cos;
        return new(cx - side / 2, cy - side / 2, side, side);
    }
}
