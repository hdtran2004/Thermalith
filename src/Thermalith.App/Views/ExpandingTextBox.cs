using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Thermalith.App.Views;

/// <summary>Grow content-entry fields with typed and wrapped lines, then scroll at MaxHeight.</summary>
public sealed class ExpandingTextBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);
    private bool _sizing;

    public ExpandingTextBox()
    {
        TextChanged += (_, _) => FitContent();
        SizeChanged += (_, _) => FitContent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        FitContent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontSizeProperty || change.Property == FontFamilyProperty
            || change.Property == FontWeightProperty || change.Property == FontStyleProperty
            || change.Property == PaddingProperty || change.Property == TextWrappingProperty)
            FitContent();
    }

    private void FitContent()
    {
        if (_sizing || Bounds.Width <= 0) return;
        _sizing = true;
        try
        {
            // Reserve room for the vertical scrollbar so wrapping stays stable at the height limit.
            var width = Math.Max(1, Bounds.Width - Padding.Left - Padding.Right - BorderThickness.Left - BorderThickness.Right - 16);
            var measure = new TextBlock
            {
                Text = (Text ?? "") + "\u200b", // include a final empty line after Enter
                FontFamily = FontFamily, FontSize = FontSize, FontWeight = FontWeight, FontStyle = FontStyle,
                TextWrapping = TextWrapping,
            };
            measure.Measure(new Size(width, double.PositiveInfinity));
            var height = Math.Clamp(Math.Ceiling(measure.DesiredSize.Height + Padding.Top + Padding.Bottom + BorderThickness.Top + BorderThickness.Bottom + 4), MinHeight, Math.Max(MinHeight, MaxHeight));
            if (double.IsNaN(Height) || Math.Abs(Height - height) > 0.5) SetCurrentValue(HeightProperty, height);
        }
        finally { _sizing = false; }
    }
}
