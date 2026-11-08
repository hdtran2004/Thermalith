using Thermalith.Core.Rendering;
using Xunit;
namespace Thermalith.Core.Tests;
public class EqualSpacingSnapTests
{
    private static readonly RenderedRect Label = new(0,0,80,60);
    [Fact]
    public void Balances_whitespace_between_neighbouring_objects()
    {
        var result = EqualSpacingSnap.Align(new RenderedRect(20.2,10,10,10),[new(0,10,10,10),new(40,10,10,10)],Label,0.3);
        Assert.Equal(-0.2,result.OffsetX,8);
        Assert.Equal(2,result.XGuides.Count);
        Assert.All(result.XGuides,g=>Assert.Equal(10,g.Gap,8));
    }
    [Theory]
    [InlineData(40.2,0,20)]
    [InlineData(0.2,20,40)]
    public void Repeats_existing_gap_before_or_after_a_row(double x,double a,double b)
    {
        var result = EqualSpacingSnap.Align(new RenderedRect(x,10,10,10),[new(a,10,10,10),new(b,10,10,10)],Label,0.3);
        Assert.Equal(-0.2,result.OffsetX,8);
        Assert.Equal(2,result.XGuides.Count);
        Assert.All(result.XGuides,g=>Assert.Equal(10,g.Gap,8));
    }
    [Fact]
    public void Balances_vertical_whitespace()
    {
        var result = EqualSpacingSnap.Align(new RenderedRect(10,20.2,10,10),[new(10,0,10,10),new(10,40,10,10)],Label,0.3);
        Assert.Equal(-0.2,result.OffsetY,8);
        Assert.Equal(2,result.YGuides.Count);
        Assert.All(result.YGuides,g=>Assert.False(g.Horizontal));
    }
    [Fact]
    public void Balances_label_margins_when_no_neighbours_exist()
    {
        var result = EqualSpacingSnap.Align(new RenderedRect(35.2,25.1,10,10),[],Label,0.3);
        Assert.Equal(-0.2,result.OffsetX,8);
        Assert.Equal(-0.1,result.OffsetY,8);
        Assert.Equal(35,result.XGuides[0].Gap,8);
        Assert.Equal(25,result.YGuides[0].Gap,8);
    }
    [Fact]
    public void Does_not_snap_to_distant_positions_or_overlapping_objects()
    {
        var distant = EqualSpacingSnap.Align(new RenderedRect(50,5,10,10),[],Label,0.3);
        Assert.Empty(distant.XGuides); Assert.Empty(distant.YGuides);
        var blocked = EqualSpacingSnap.Align(new RenderedRect(10.2,10,10,10),[new(0,10,15,10),new(35,10,15,10)],Label,0.3);
        Assert.Empty(blocked.XGuides);
    }
    [Fact]
    public void Enclosing_frame_does_not_block_equal_spacing()
    {
        var frame = new RenderedRect(1,1,78,58);
        var result = EqualSpacingSnap.Align(new RenderedRect(20.2,10,10,10),[frame,new(0,10,10,10),new(40,10,10,10)],Label,0.3);
        Assert.Equal(-0.2,result.OffsetX,8);
        Assert.Equal(2,result.XGuides.Count);
    }}
