namespace Thermalith.Core.Rendering;

public sealed record AlignmentGuide(bool Vertical, double Position, double Start, double End);
public sealed record SmartSnapResult(double OffsetX, double OffsetY, AlignmentGuide? XGuide, AlignmentGuide? YGuide);

/// <summary>Find nearby edge/centre alignments, preferring the closest neighbour when distances tie.</summary>
public static class SmartSnap
{
    public static SmartSnapResult Align(RenderedRect moving, IReadOnlyList<RenderedRect> targets, double tolerance)
    {
        (double Offset, RenderedRect Target, double Position)? Find(bool vertical)
        {
            (double Offset, RenderedRect Target, double Position)? best = null;
            var bestDistance = tolerance;
            var bestNeighbour = double.PositiveInfinity;
            var source = vertical ? moving.XMm : moving.YMm;
            var size = vertical ? moving.WMm : moving.HMm;
            foreach (var target in targets)
            {
                var targetStart = vertical ? target.XMm : target.YMm;
                var targetSize = vertical ? target.WMm : target.HMm;
                var neighbour = vertical ? Math.Abs((moving.YMm + moving.HMm / 2) - (target.YMm + target.HMm / 2)) : Math.Abs((moving.XMm + moving.WMm / 2) - (target.XMm + target.WMm / 2));
                foreach (var from in new[] { source, source + size / 2, source + size })
                    foreach (var to in new[] { targetStart, targetStart + targetSize / 2, targetStart + targetSize })
                    {
                        var delta = to - from;
                        var distance = Math.Abs(delta);
                        if (distance > tolerance) continue;
                        if (distance < bestDistance - 1e-9 || Math.Abs(distance - bestDistance) <= 1e-9 && neighbour < bestNeighbour)
                        {
                            bestDistance = distance; bestNeighbour = neighbour; best = (delta, target, to);
                        }
                    }
            }
            return best;
        }
        var x = Find(true); var y = Find(false);
        var dx = x?.Offset ?? 0; var dy = y?.Offset ?? 0;
        var xGuide = x is { } vx ? new AlignmentGuide(true, vx.Position, Math.Min(moving.YMm + dy, vx.Target.YMm), Math.Max(moving.YMm + dy + moving.HMm, vx.Target.YMm + vx.Target.HMm)) : null;
        var yGuide = y is { } hy ? new AlignmentGuide(false, hy.Position, Math.Min(moving.XMm + dx, hy.Target.XMm), Math.Max(moving.XMm + dx + moving.WMm, hy.Target.XMm + hy.Target.WMm)) : null;
        return new SmartSnapResult(dx, dy, xGuide, yGuide);
    }
}
