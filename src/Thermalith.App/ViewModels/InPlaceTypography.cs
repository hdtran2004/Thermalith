using Avalonia.Media;
using Avalonia.Layout;

namespace Thermalith.App.ViewModels;

public sealed record InPlaceTypography
{
    public FontFamily FontFamily { get; init; } = new("Arial");
    public double FontSize { get; init; } = 11;
    public FontWeight FontWeight { get; init; } = FontWeight.Normal;
    public FontStyle FontStyle { get; init; } = FontStyle.Normal;
    public TextAlignment TextAlignment { get; init; } = TextAlignment.Left;
    public VerticalAlignment VerticalAlignment { get; init; } = VerticalAlignment.Top;
}
