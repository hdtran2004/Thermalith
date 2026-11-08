namespace Thermalith.Core.Catalog;

public static class StockCompatibility
{
    // Compare feed dimensions after rotation, allowing only measurement rounding.
    public static bool SizeMatches(double designWidth, double designHeight, int rotation, RollDefinition loaded)
    {
        if (((rotation % 360) + 360) % 360 is 90 or 270)
            (designWidth, designHeight) = (designHeight, designWidth);
        return Math.Abs(designWidth - loaded.WidthMm) <= 0.1 &&
               Math.Abs(designHeight - loaded.HeightMm) <= 0.1;
    }
}
