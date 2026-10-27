namespace Thermalith.Core.Rendering;

/// <summary>Layout offsets in document millimetres, independent of grid snapping and print DPI.</summary>
public static class FormAlignment
{
    /// <summary>Translate rendered bounds so their midpoint is at the label midpoint.</summary>
    public static double CenterOffset(double labelSizeMm, double contentStartMm, double contentSizeMm) =>
        (labelSizeMm - contentSizeMm) / 2.0 - contentStartMm;
}
