using Thermalith.Core.Catalog;
using Xunit;
namespace Thermalith.Core.Tests;
public class StockCompatibilityTests
{
    [Theory]
    [InlineData(80, 50, 0, true)]
    [InlineData(50, 80, 90, true)]
    [InlineData(50, 80, 270, true)]
    [InlineData(80, 50, 180, true)]
    [InlineData(50, 80, 0, false)]
    [InlineData(80, 40, 0, false)]
    [InlineData(80.05, 50, 0, true)]
    public void Dimensions_determine_compatibility_independent_of_material(double w, double h, int rotation, bool expected)
    {
        foreach (var type in new[] { "gap", "transparent" })
            Assert.Equal(expected, StockCompatibility.SizeMatches(w, h, rotation,
                new RollDefinition { Name = "Alternative stock", PaperType = type, WidthMm = 80, HeightMm = 50 }));
    }
}
