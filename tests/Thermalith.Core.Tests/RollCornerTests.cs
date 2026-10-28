using Thermalith.Core.Catalog;
using Xunit;
namespace Thermalith.Core.Tests;
public class RollCornerTests
{
    [Fact]
    public void Existing_rounded_roll_has_default_radius_and_custom_radius_is_bounded()
    {
        var roll = new RollDefinition { WidthMm = 30, HeightMm = 20, Shape = "rounded" };
        Assert.Equal(2, roll.EffectiveCornerRadiusMm);
        Assert.Equal(3, (roll with { CornerRadiusMm = 3 }).EffectiveCornerRadiusMm);
        Assert.Equal(10, (roll with { CornerRadiusMm = 30 }).EffectiveCornerRadiusMm);
        Assert.Equal(0, (roll with { Shape = "rectangle" }).EffectiveCornerRadiusMm);
        Assert.Equal(0, (roll with { CornerRadiusMm = 0 }).EffectiveCornerRadiusMm);
    }
}
