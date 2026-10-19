using System.Windows;
using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// Draws every LED as a dot, wrapping to the available width. One element per device instead of
// one per LED keeps a 104-key keyboard cheap to redraw.
public sealed class LedStripView : FrameworkElement
{
    public static readonly DependencyProperty ColorsProperty = DependencyProperty.Register(
        nameof(Colors),
        typeof(IReadOnlyList<RgbColor>),
        typeof(LedStripView),
        new FrameworkPropertyMetadata(Array.Empty<RgbColor>(), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    private const double LedSize = 14.0;
    private const double Spacing = 4.0;
    private const double Pitch = LedSize + Spacing;

    private static readonly Pen Outline = CreateOutline();

    public IReadOnlyList<RgbColor> Colors
    {
        get => (IReadOnlyList<RgbColor>)GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = Colors.Count;
        if (count == 0)
        {
            return default;
        }

        var columns = ColumnsFor(availableSize.Width, count);
        var rows = (count + columns - 1) / columns;
        return new Size(columns * Pitch - Spacing, rows * Pitch - Spacing);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var colors = Colors;
        var columns = ColumnsFor(ActualWidth, colors.Count);
        const double radius = LedSize / 2.0;
        for (var i = 0; i < colors.Count; i++)
        {
            var center = new Point((i % columns) * Pitch + radius, (i / columns) * Pitch + radius);
            drawingContext.DrawEllipse(ColorBrushes.Get(colors[i]), Outline, center, radius, radius);
        }
    }

    private static int ColumnsFor(double width, int count)
    {
        if (double.IsInfinity(width) || double.IsNaN(width))
        {
            return Math.Max(count, 1);
        }

        return Math.Clamp((int)((width + Spacing) / Pitch), 1, Math.Max(count, 1));
    }

    private static Pen CreateOutline()
    {
        var pen = new Pen(new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x60, 0x80, 0x80, 0x80)), 1.0);
        pen.Freeze();
        return pen;
    }
}
