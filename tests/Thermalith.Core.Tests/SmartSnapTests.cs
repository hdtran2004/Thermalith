using Thermalith.Core.Rendering;
using Xunit;
namespace Thermalith.Core.Tests;
public class SmartSnapTests
{
    [Fact]
    public void Aligns_edges_and_centres_independently()
    {
        var result = SmartSnap.Align(new RenderedRect(10.2,20.1,10,10), [new RenderedRect(10,40,10,10),new RenderedRect(40,20,10,10)],0.3);
        Assert.Equal(-0.2,result.OffsetX,8);
        Assert.Equal(-0.1,result.OffsetY,8);
        Assert.NotNull(result.XGuide); Assert.NotNull(result.YGuide);
    }
    [Fact]
    public void Snaps_adjacent_edges_and_rejects_distant_alignments()
    {
        var result = SmartSnap.Align(new RenderedRect(20.2,60,10,10),[new RenderedRect(10,10,10,10)],0.3);
        Assert.Equal(-0.2,result.OffsetX,8);
        Assert.Null(result.YGuide);
        var none = SmartSnap.Align(new RenderedRect(24,60,10,10),[new RenderedRect(10,10,10,10)],0.3);
        Assert.Null(none.XGuide); Assert.Null(none.YGuide);
    }
    [Fact]
    public void Tied_alignment_prefers_nearest_neighbour()
    {
        var result = SmartSnap.Align(new RenderedRect(10.1,20,10,10),[new RenderedRect(10,100,10,10),new RenderedRect(10,40,10,10)],0.2);
        Assert.NotNull(result.XGuide);
        Assert.Equal(50,result.XGuide.End);
    }
}
