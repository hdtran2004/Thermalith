using Avalonia.Media;

namespace Thermalith.App.ViewModels.Editors;

/// <summary>Enum value lists backing the inspector dropdowns (mirrors the schema, label-json-spec §11).</summary>
public static class EditorOptions
{
    /// <summary>Sentinel for "use the app's bundled font" — maps to a null FontFamily on the element.</summary>
    public const string BundledFont = "(bundled)";

    private static string[]? _fonts;

    /// <summary>Bundled sentinel followed by installed system font families (cross-platform via Avalonia's
    /// <see cref="FontManager"/>). Built once on first access, after Avalonia is initialised.</summary>
    public static string[] Fonts => _fonts ??= BuildFonts();

    private static string[] BuildFonts()
    {
        try
        {
            var system = FontManager.Current.SystemFonts
                .Select(f => f.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
            return new[] { BundledFont }.Concat(system).ToArray();
        }
        catch
        {
            return [BundledFont]; // headless / FontManager unavailable
        }
    }

    public static string[] CellFonts => new[] { "(table default)", "Roboto" }
        .Concat(Fonts.Where(f => f != BundledFont && f != "Roboto")).ToArray();
    public static string[] QrCaptionFonts => new[] { "Roboto" }
        .Concat(Fonts.Where(f => f != BundledFont && f != "Roboto")).ToArray();
    public static Avalonia.Controls.ComboBoxItem[] FontItems => FontMenu(Fonts);
    public static Avalonia.Controls.ComboBoxItem[] CellFontItems => FontMenu(CellFonts);
    public static Avalonia.Controls.ComboBoxItem[] QrCaptionFontItems => FontMenu(QrCaptionFonts);

    private static List<string>? _recentFonts;
    private static string RecentFontPath => Path.Combine(Thermalith.App.Services.PlatformDirectories.AppData(), "recent-fonts.json");
    private static List<string> RecentFonts
    {
        get
        {
            if (_recentFonts is not null) return _recentFonts;
            try
            {
                _recentFonts = (System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentFontPath)) ?? [])
                    .Where(f => !string.IsNullOrWhiteSpace(f) && !f.StartsWith("("))
                    .Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList();
            }
            catch { _recentFonts = []; }
            return _recentFonts;
        }
    }

    public static void RememberFont(string font)
    {
        if (string.IsNullOrWhiteSpace(font) || font.StartsWith("(")) return;
        var recent = RecentFonts;
        recent.RemoveAll(f => string.Equals(f, font, StringComparison.OrdinalIgnoreCase));
        recent.Insert(0, font);
        if (recent.Count > 5) recent.RemoveRange(5, recent.Count - 5);
        try { File.WriteAllText(RecentFontPath, System.Text.Json.JsonSerializer.Serialize(recent)); }
        catch { /* Read-only profiles must not prevent selecting a font. */ }
    }

    private static Avalonia.Controls.ComboBoxItem[] FontMenu(IEnumerable<string> fonts)
    {
        var all = fonts.ToList();
        var recent = RecentFonts.Where(f => all.Contains(f, StringComparer.OrdinalIgnoreCase)).ToList();
        var items = new List<Avalonia.Controls.ComboBoxItem>();
        if (recent.Count > 0)
        {
            items.Add(new() { Content = "Recent fonts", IsEnabled = false, FontWeight = Avalonia.Media.FontWeight.Bold });
            items.AddRange(recent.Select(f => new Avalonia.Controls.ComboBoxItem { Content = f }));
        }
        items.Add(new() { Content = "All fonts", IsEnabled = false, FontWeight = Avalonia.Media.FontWeight.Bold });
        items.AddRange(all.Select(f => new Avalonia.Controls.ComboBoxItem { Content = f }));
        return items.ToArray();
    }

    public static string[] QrTextPositions { get; } = ["above", "below"];
    public static string[] QrTextAlignments { get; } = ["left", "center", "right"];
    public static string[] JustifyH { get; } = ["left", "center", "right", "justify"];
    public static string[] JustifyV { get; } = ["top", "middle", "bottom"];
    public static string[] Wrap { get; } = ["none", "word"];
    public static string[] Symbology { get; } = ["code128", "code39", "ean13", "ean8", "upca", "upce", "itf", "codabar"];
    public static string[] TextPosition { get; } = ["above", "below", "none"];
    public static string[] QrEncoding { get; } = ["text", "hex"];
    public static string[] EcLevel { get; } = ["L", "M", "Q", "H"];
    public static string[] ShapeType { get; } = ["rect", "roundedRect", "ellipse"];
    public static string[] Fill { get; } = ["none", "solid"];
    public static string[] Fit { get; } = ["fill", "fit", "stretch", "center"];
    public static string[] Dither { get; } = ["threshold", "floydSteinberg", "atkinson", "ordered", "none"];
    public static string[] DateKind { get; } = ["date", "time", "datetime"];
    public static string[] DateSource { get; } = ["printNow", "fixed"];
    public static string[] CanvasShape { get; } = ["rectangle", "rounded", "circle", "dieCut"];
    public static int[] Orientation { get; } = [0, 90, 180, 270];
    public static string[] PaperType { get; } = ["gap", "black", "continuous", "transparent", "perforated"];
}
