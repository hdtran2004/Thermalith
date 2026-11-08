using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Thermalith.App.ViewModels;

namespace Thermalith.App.Views;

/// <summary>Clean print layout: artwork only, with resolved next values and no editing overlays.</summary>
public sealed class PrintPreviewDialog : Window
{
    private Bitmap? _bitmap;
    public PrintPreviewDialog(EditorViewModel editor, int copies, bool canPrint)
    {
        Title = "Print preview — " + editor.DocumentName;
        Width = 1000; Height = 730; MinWidth = 600; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(16) };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0,0,0,12) };
        copies = Math.Max(1,copies);
        toolbar.Children.Add(new TextBlock { Text = "Label", VerticalAlignment = VerticalAlignment.Center });
        var page = new NumericUpDown { Minimum = 1, Maximum = copies, Value = 1, FormatString = "0", Width = 140 };
        toolbar.Children.Add(page);
        toolbar.Children.Add(new TextBlock { Text = $"of {copies}", VerticalAlignment = VerticalAlignment.Center });
        var dots = new CheckBox { Content = "Show printer dots", IsChecked = true, Margin = new Thickness(16,0,0,0) };
        toolbar.Children.Add(dots);
        DockPanel.SetDock(toolbar,Dock.Top); root.Children.Add(toolbar);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0,12,0,0) };
        var canvas = editor.PrintPreviewCanvas;
        footer.Children.Add(new TextBlock { Text = $"{canvas.WidthMm:0.#} × {canvas.HeightMm:0.#} mm · {canvas.Dpi} dpi", VerticalAlignment = VerticalAlignment.Center });
        var close = new Button { Content = "Close", IsCancel = true, Margin = new Thickness(8,0) }; close.Click += (_,_) => Close(false);
        Grid.SetColumn(close,1); footer.Children.Add(close);
        var print = new Button { Content = "Print…", IsEnabled = canPrint }; print.Classes.Add("accent"); print.Click += (_,_) => Close(true);
        Grid.SetColumn(print,2); footer.Children.Add(print);
        DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
        var image = new Image { Stretch = Stretch.Fill };
        var viewbox = new Viewbox { Child = image, Stretch = Stretch.Uniform, Margin = new Thickness(24) };
        root.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#383B40")), CornerRadius = new CornerRadius(8), Child = viewbox });
        Content = root;
        void Refresh()
        {
            var next = editor.RenderPrintPreview((int)(page.Value ?? 1) - 1, dots.IsChecked == true);
            var previous = _bitmap; _bitmap = next;
            image.Source = next; image.Width = next.PixelSize.Width; image.Height = next.PixelSize.Height;
            var rect = new Rect(0,0,image.Width,image.Height);
            if (canvas.Shape == "circle") image.Clip = new EllipseGeometry(rect);
            else
            {
                var radius = canvas.Shape == "rounded" ? Math.Min(Math.Min(image.Width,image.Height) / 2, canvas.CornerRadiusMm / canvas.WidthMm * image.Width) : 0;
                image.Clip = new RectangleGeometry(rect, radius, radius);
            }
            RenderOptions.SetBitmapInterpolationMode(image, dots.IsChecked == true ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality);
            previous?.Dispose();
        }
        page.ValueChanged += (_,_) => Refresh(); dots.IsCheckedChanged += (_,_) => Refresh();
        Closed += (_,_) => { image.Source = null; _bitmap?.Dispose(); };
        Refresh();
    }
}
