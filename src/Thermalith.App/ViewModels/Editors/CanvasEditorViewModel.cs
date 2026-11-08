using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Thermalith.Core.Model;

namespace Thermalith.App.ViewModels.Editors;

/// <summary>
/// Editable view of the canvas + document name, shown in the inspector when no element is selected
/// (build spec §6.1/§7). Edits the physical size, DPI, shape, orientation, bleed and safe-area inset
/// (§6.1.3). Any change invokes the supplied callback, which rebuilds the document and re-renders.
/// </summary>
public sealed partial class CanvasEditorViewModel : ObservableObject
{
    private readonly Action _onChanged;
    private bool _loaded;
    private Thermalith.Core.Catalog.RollDefinition? _stock;
    private Thermalith.Core.Catalog.RollDefinition[] _stocks;
    public string[] StockChoices => _stocks.Select(s => s.Name).Concat(string.IsNullOrWhiteSpace(StockName) ? Array.Empty<string>() : new[] { StockName }).Distinct().ToArray();

    public CanvasEditorViewModel(LabelDocument doc, Action onChanged)
    {
        _onChanged = onChanged;
        var c = doc.Canvas;
        _stock = c.Stock;
        var catalog = new Thermalith.App.Services.LabelRollStore().Catalog;
        _stocks = catalog.Rolls.Concat(c.Stock is { } saved ? new[] { saved } : Array.Empty<Thermalith.Core.Catalog.RollDefinition>()).GroupBy(s => s.Name).Select(g => g.Last()).ToArray();
        _name = doc.Metadata.Name;
        _stockName = doc.Canvas.StockName ?? "";
        _widthMm = c.WidthMm;
        _heightMm = c.HeightMm;
        _dpi = c.Dpi;
        _shape = c.Shape;
        _cornerRadiusMm = c.CornerRadiusMm;
        _bleedMm = c.BleedMm;
        _printheadWidthMm = c.PrintheadWidthMm;
        _orientationDeg = c.OrientationDeg;
        _safeMarginMm = c.SafeAreaInsetMm ?? 0;
        _loaded = true;
    }

    [ObservableProperty] private bool _readOnlyContext;
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _stockName;
    [ObservableProperty] private double _widthMm;
    [ObservableProperty] private double _heightMm;
    [ObservableProperty] private int _dpi;
    [ObservableProperty] private string _shape;
    [ObservableProperty] private double _cornerRadiusMm;
    [ObservableProperty] private double _bleedMm;
    [ObservableProperty] private double? _printheadWidthMm;
    [ObservableProperty] private int _orientationDeg;
    private int _previousOrientation;
    partial void OnOrientationDegChanging(int value) => _previousOrientation = OrientationDeg;

    /// <summary>User-set soft safe margin (mm) inset on every edge — a guide to keep content clear of
    /// per-printer skew/registration. 0 = off. Guide only: it never crops, the user may place outside it.</summary>
    [ObservableProperty] private double _safeMarginMm;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        // Selecting rounded must produce visible corners even when the previous rectangle had radius 0.
        // Notify the owner once, after both shape and radius have changed.
        if (_loaded && e.PropertyName == nameof(Shape) && Shape == "rounded" && CornerRadiusMm <= 0)
        {
            _loaded = false;
            try { CornerRadiusMm = Math.Min(2, Math.Max(0, Math.Min(WidthMm, HeightMm) / 2)); }
            finally { _loaded = true; }
        }
        if (_loaded && e.PropertyName == nameof(StockName) && _stocks.FirstOrDefault(s => s.Name == StockName) is { } stock)
        {
            var portrait = Layout == "Portrait";
            _loaded = false;
            try
            {
                _stock = stock;
                WidthMm = portrait ? Math.Min(stock.WidthMm, stock.HeightMm) : Math.Max(stock.WidthMm, stock.HeightMm);
                HeightMm = portrait ? Math.Max(stock.WidthMm, stock.HeightMm) : Math.Min(stock.WidthMm, stock.HeightMm);
                Shape = stock.Shape; CornerRadiusMm = stock.EffectiveCornerRadiusMm;
                OrientationDeg = Math.Abs(WidthMm - stock.WidthMm) < 0.1 ? 0 : 90;
            }
            finally { _loaded = true; }
        }
        if (_loaded && e.PropertyName == nameof(OrientationDeg))
        {
            var oldQuarterTurn = ((_previousOrientation % 180) + 180) % 180;
            var newQuarterTurn = ((OrientationDeg % 180) + 180) % 180;
            _loaded = false;
            try
            {
                if (oldQuarterTurn != newQuarterTurn)
                {
                    var width = WidthMm; WidthMm = HeightMm; HeightMm = width;
                    OnPropertyChanged(nameof(Layout));
                }
            }
            finally { _loaded = true; }
        }
        base.OnPropertyChanged(e);
        if (_loaded) _onChanged();
    }

    public string[] LayoutChoices { get; } = ["Landscape", "Portrait"];
    public string Layout
    {
        get => WidthMm >= HeightMm ? "Landscape" : "Portrait";
        set
        {
            if (value == Layout) return;
            _loaded = false;
            try { var width = WidthMm; WidthMm = HeightMm; HeightMm = width; OrientationDeg = (OrientationDeg + 90) % 180; }
            finally { _loaded = true; }
            OnPropertyChanged(nameof(Layout));
        }
    }

    public void UpdateStock(Thermalith.Core.Catalog.RollDefinition stock)
    {
        var previous = StockName;
        var portrait = Layout == "Portrait";
        _loaded = false;
        try
        {
            _stock = stock;
            _stocks = _stocks.Where(s => s.Name != previous && s.Name != stock.Name).Append(stock).ToArray();
            StockName = stock.Name;
            WidthMm = portrait ? Math.Min(stock.WidthMm,stock.HeightMm) : Math.Max(stock.WidthMm,stock.HeightMm);
            HeightMm = portrait ? Math.Max(stock.WidthMm,stock.HeightMm) : Math.Min(stock.WidthMm,stock.HeightMm);
            Shape = stock.Shape; CornerRadiusMm = stock.EffectiveCornerRadiusMm;
            OrientationDeg = Math.Abs(WidthMm - stock.WidthMm) < 0.1 ? 0 : 90;
            OnPropertyChanged(nameof(StockChoices));
        }
        finally { _loaded = true; }
        OnPropertyChanged(nameof(Layout));
    }

    public LabelDocument Apply(LabelDocument doc) => doc with
    {
        Metadata = doc.Metadata with { Name = Name, ModifiedUtc = DateTimeOffset.UtcNow.ToString("o") },
        Canvas = doc.Canvas with
        {
            WidthMm = WidthMm <= 0 ? doc.Canvas.WidthMm : WidthMm,
            HeightMm = HeightMm <= 0 ? doc.Canvas.HeightMm : HeightMm,
            Dpi = Dpi <= 0 ? doc.Canvas.Dpi : Dpi,
            Stock = _stock,
            StockName = string.IsNullOrWhiteSpace(StockName) ? null : StockName.Trim(),
            Shape = Shape,
            CornerRadiusMm = CornerRadiusMm,
            BleedMm = BleedMm,
            PrintheadWidthMm = PrintheadWidthMm,
            OrientationDeg = OrientationDeg,
            SafeAreaInsetMm = SafeMarginMm > 0 ? SafeMarginMm : null, // 0 → omit (off)
        },
    };
}
