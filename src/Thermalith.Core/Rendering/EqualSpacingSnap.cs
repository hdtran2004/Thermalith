namespace Thermalith.Core.Rendering;

public sealed record SpacingGuide(bool Horizontal, double Start, double End, double Position)
{
    public double Gap => End - Start;
}
public sealed record EqualSpacingResult(double OffsetX, double OffsetY, IReadOnlyList<SpacingGuide> XGuides, IReadOnlyList<SpacingGuide> YGuides);

/// <summary>Match clear gaps on the same row/column, or balance whitespace between neighbours and label edges.</summary>
public static class EqualSpacingSnap
{
    public static EqualSpacingResult Align(RenderedRect moving, IReadOnlyList<RenderedRect> targets, RenderedRect label, double tolerance)
    {
        // An enclosing frame/background defines whitespace around its contents, rather than blocking it.
        var enclosing = targets.Where(r => r.WMm * r.HMm > moving.WMm * moving.HMm + 1e-6
            && r.XMm <= moving.XMm && r.YMm <= moving.YMm
            && r.XMm + r.WMm >= moving.XMm + moving.WMm && r.YMm + r.HMm >= moving.YMm + moving.HMm)
            .OrderBy(r => r.WMm * r.HMm).ToList();
        var boundary = enclosing.Count > 0 ? enclosing[0] : label;
        var obstacles = targets.Where(r => !enclosing.Contains(r)).ToList();
        (double Offset, IReadOnlyList<SpacingGuide> Guides) Find(bool horizontal)
        {
            double Start(RenderedRect r) => horizontal ? r.XMm : r.YMm;
            double Size(RenderedRect r) => horizontal ? r.WMm : r.HMm;
            double CrossStart(RenderedRect r) => horizontal ? r.YMm : r.XMm;
            double CrossSize(RenderedRect r) => horizontal ? r.HMm : r.WMm;
            var position = CrossStart(moving) + CrossSize(moving) / 2;
            var neighbours = obstacles.Where(r => Math.Min(CrossStart(r) + CrossSize(r), CrossStart(moving) + CrossSize(moving)) - Math.Max(CrossStart(r),CrossStart(moving)) > 1e-6)
                .OrderBy(Start).ToList();
            var source = Start(moving); var size = Size(moving);
            var best = double.PositiveInfinity;
            IReadOnlyList<SpacingGuide> bestGuides = [];
            SpacingGuide Guide(double start,double end) => new(horizontal,start,end,position);
            void Candidate(double start, params SpacingGuide[] guides)
            {
                var delta = start - source;
                if (Math.Abs(delta) > tolerance || Math.Abs(delta) >= Math.Abs(best) - 1e-9) return;
                if (start < Start(boundary) - 1e-6 || start + size > Start(boundary) + Size(boundary) + 1e-6) return;
                if (neighbours.Any(r => Math.Min(start + size,Start(r) + Size(r)) - Math.Max(start,Start(r)) > 1e-6)) return;
                if (guides.Any(g => g.Gap <= 1e-6)) return;
                best = delta; bestGuides = guides;
            }
            var before = neighbours.Where(r => Start(r) + Size(r) <= source + tolerance).OrderByDescending(r=>Start(r)+Size(r)).ToList();
            var after = neighbours.Where(r => Start(r) >= source + size - tolerance).ToList();
            var left = before.Count > 0 ? Start(before[0]) + Size(before[0]) : Start(boundary);
            var right = after.Count > 0 ? Start(after[0]) : Start(boundary) + Size(boundary);
            if (right - left >= size)
            {
                var balanced = (left + right - size) / 2;
                Candidate(balanced,Guide(left,balanced),Guide(balanced + size,right));
            }
            for (var i = 0; i + 1 < neighbours.Count; i++)
            {
                var a = neighbours[i]; var b = neighbours[i+1];
                var aEnd = Start(a) + Size(a); var bStart = Start(b); var bEnd = bStart + Size(b);
                var gap = bStart - aEnd;
                if (gap <= 1e-6) continue;
                var next = bEnd + gap;
                Candidate(next,Guide(aEnd,bStart),Guide(bEnd,next));
                var previous = Start(a) - gap - size;
                Candidate(previous,Guide(previous + size,Start(a)),Guide(aEnd,bStart));
            }
            return (double.IsFinite(best) ? best : 0,bestGuides);
        }
        var x = Find(true); var y = Find(false);
        return new EqualSpacingResult(x.Offset,y.Offset,x.Guides,y.Guides);
    }
}
