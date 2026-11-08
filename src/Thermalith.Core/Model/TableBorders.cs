namespace Thermalith.Core.Model;

public sealed record CellBorder
{
    public double WidthMm { get; init; } = 0.1;
    public string Style { get; init; } = "solid";
}
public sealed record CellBorders
{
    public CellBorder? Top { get; init; }
    public CellBorder? Bottom { get; init; }
    public CellBorder? Left { get; init; }
    public CellBorder? Right { get; init; }
}

public static class TableBorderFormatting
{
    public static TableProps Apply(TableProps props, int r0, int c0, int r1, int c1, string preset, CellBorder border)
    {
        if (props.Rows <= 0 || props.Cols <= 0) return props;
        r0 = Math.Clamp(r0,0,props.Rows-1); r1 = Math.Clamp(r1,r0,props.Rows-1);
        c0 = Math.Clamp(c0,0,props.Cols-1); c1 = Math.Clamp(c1,c0,props.Cols-1);
        var cells = Enumerable.Range(0,props.Rows).Select(r => Enumerable.Range(0,props.Cols)
            .Select(c => props.Cells is { } source && r < source.Count && c < source[r].Count ? source[r][c] : new TableCell()).ToList()).ToList();
        var edge = preset == "none" ? border with { WidthMm = 0 } : border;
        (int R, int C) Anchor(int r,int c)
        {
            for (var y = 0; y <= r; y++)
                for (var x = 0; x <= c; x++)
                {
                    var cell = cells[y][x];
                    if ((cell.RowSpan > 1 || cell.ColSpan > 1) && y + cell.RowSpan > r && x + cell.ColSpan > c) return (y,x);
                }
            return (r,c);
        }
        void Set(int r, int c, string side)
        {
            if (r < 0 || c < 0 || r >= props.Rows || c >= props.Cols) return;
            var (ar,ac) = Anchor(r,c);
            var current = cells[ar][ac]; var edges = current.Borders ?? new CellBorders();
            edges = side switch { "top" => edges with { Top = edge }, "bottom" => edges with { Bottom = edge }, "left" => edges with { Left = edge }, _ => edges with { Right = edge } };
            cells[ar][ac] = current with { Borders = edges };
        }
        for (var r = r0; r <= r1 + 1; r++)
            for (var c = c0; c <= c1; c++)
            {
                var outside = r == r0 || r == r1 + 1;
                if (!(preset is "all" or "none" || preset == "outer" && outside || preset == "inner" && !outside || preset == "top" && r == r0 || preset == "bottom" && r == r1 + 1)) continue;
                if (r > 0 && r < props.Rows && Anchor(r,c) == Anchor(r-1,c)) continue;
                Set(r,c,"top"); Set(r-1,c,"bottom");
            }
        for (var c = c0; c <= c1 + 1; c++)
            for (var r = r0; r <= r1; r++)
            {
                var outside = c == c0 || c == c1 + 1;
                if (!(preset is "all" or "none" || preset == "outer" && outside || preset == "inner" && !outside || preset == "left" && c == c0 || preset == "right" && c == c1 + 1)) continue;
                if (c > 0 && c < props.Cols && Anchor(r,c) == Anchor(r,c-1)) continue;
                Set(r,c,"left"); Set(r,c-1,"right");
            }
        return props with { Cells = cells };
    }
}
