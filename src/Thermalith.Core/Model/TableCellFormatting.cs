namespace Thermalith.Core.Model;

public static class TableCellFormatting
{
    /// <summary>Copy appearance only. Target data, merge spans, and extensions remain intact.</summary>
    public static TableCell Apply(TableCell source, TableCell target) => target with
    {
        FontFamily = source.FontFamily, FontSizePt = source.FontSizePt,
        Bold = source.Bold, Italic = source.Italic, Underline = source.Underline, Justify = source.Justify,
        Borders = source.Borders,
        Fill = source.Fill, TextColor = source.TextColor,
    };
}
