using System.Windows;
using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// The small strip in the head of a device card on Kleur (DESIGN.md § 9.3): a row of bulbs in Colors,
// evenly spread over its width, drawn by one element instead of a LedBulb per bulb. A card shows
// what its device does on every frame, also closed, so twelve bulbs on every card would be a dozen
// elements to bind and draw per card and frame; this is one. Each bulb looks as a LedBulb: a lit
// one with its glow (from the cache LedBulb keeps), a black one in OffBrush with the ring of
// OffRingBrush inside its edge. Only decoration: UI Automation does not see it.
public sealed class SmallLedStrip : FrameworkElement
{
    public static readonly DependencyProperty ColorsProperty = DependencyProperty.Register(
        nameof(Colors), typeof(IReadOnlyList<RgbColor>), typeof(SmallLedStrip), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DiameterProperty = DependencyProperty.Register(
        nameof(Diameter), typeof(double), typeof(SmallLedStrip), new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlowBlurProperty = DependencyProperty.Register(
        nameof(GlowBlur), typeof(double), typeof(SmallLedStrip), new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlowSpreadProperty = DependencyProperty.Register(
        nameof(GlowSpread), typeof(double), typeof(SmallLedStrip), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OffBrushProperty = DependencyProperty.Register(
        nameof(OffBrush), typeof(Brush), typeof(SmallLedStrip), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OffRingBrushProperty = DependencyProperty.Register(
        nameof(OffRingBrush), typeof(Brush), typeof(SmallLedStrip), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<RgbColor>? Colors
    {
        get => (IReadOnlyList<RgbColor>?)GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
    }

    public double Diameter
    {
        get => (double)GetValue(DiameterProperty);
        set => SetValue(DiameterProperty, value);
    }

    public double GlowBlur
    {
        get => (double)GetValue(GlowBlurProperty);
        set => SetValue(GlowBlurProperty, value);
    }

    public double GlowSpread
    {
        get => (double)GetValue(GlowSpreadProperty);
        set => SetValue(GlowSpreadProperty, value);
    }

    public Brush? OffBrush
    {
        get => (Brush?)GetValue(OffBrushProperty);
        set => SetValue(OffBrushProperty, value);
    }

    public Brush? OffRingBrush
    {
        get => (Brush?)GetValue(OffRingBrushProperty);
        set => SetValue(OffRingBrushProperty, value);
    }

    // The middle of bulb index of count over width.
    internal static double CenterOf(int index, int count, double width) => (index + 0.5) * width / count;

    protected override Size MeasureOverride(Size availableSize) => new(0, Diameter);

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (Colors is not { Count: > 0 } colors)
        {
            return;
        }

        var radius = Diameter / 2.0;
        var glowRadius = radius + GlowSpread + GlowBlur;
        var ring = LedRing.PenOf(OffRingBrush);
        var offRadius = ring is null ? radius : Math.Max(0.0, radius - ring.Thickness / 2.0);
        var y = ActualHeight / 2.0;
        for (var i = 0; i < colors.Count; i++)
        {
            var center = new Point(CenterOf(i, colors.Count, ActualWidth), y);
            var color = colors[i];
            if (LedRing.IsDark(color))
            {
                drawingContext.DrawEllipse(OffBrush, ring, center, offRadius, offRadius);
                continue;
            }

            drawingContext.DrawEllipse(LedBulb.GlowOf(Diameter, GlowBlur, GlowSpread, color), null, center, glowRadius, glowRadius);
            drawingContext.DrawEllipse(ColorBrushes.Get(color), null, center, radius, radius);
        }
    }
}
