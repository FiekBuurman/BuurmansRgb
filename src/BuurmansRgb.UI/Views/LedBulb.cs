using System.Windows;
using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// An LED of the design (§ 4): a dot in its color with a glow around it, like the CSS box-shadow
// "0 0 <GlowBlur> <GlowSpread> <color>". The glow is a radial gradient drawn past the bounds
// instead of a DropShadowEffect, which would render every bulb off screen on each frame. Off, it
// shows OffBrush (led-off of the tokens, set by its style in Controls.xaml) without a glow. Only
// decoration: what it shows is also said in text nearby.
//
// The glows are cached by size and color for all bulbs, with the color rounded to 5 bits per
// channel, which a soft glow does not show: an animation then shows the same few hundred glows
// over and over, where a new gradient per frame cost ~2 MB/s on Lampen with the rainbow. An
// opacity mask per size instead allocated less but doubled the CPU of drawing.
public sealed class LedBulb : FrameworkElement
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(RgbColor), typeof(LedBulb), new FrameworkPropertyMetadata(RgbColor.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OffBrushProperty = DependencyProperty.Register(
        nameof(OffBrush), typeof(Brush), typeof(LedBulb), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsLitProperty = DependencyProperty.Register(
        nameof(IsLit), typeof(bool), typeof(LedBulb), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DiameterProperty = DependencyProperty.Register(
        nameof(Diameter), typeof(double), typeof(LedBulb), new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlowBlurProperty = DependencyProperty.Register(
        nameof(GlowBlur), typeof(double), typeof(LedBulb), new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GlowSpreadProperty = DependencyProperty.Register(
        nameof(GlowSpread), typeof(double), typeof(LedBulb), new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    internal const int MaxCachedGlows = 2048;

    // UI thread only.
    private static readonly Dictionary<(double Diameter, double Blur, double Spread, RgbColor Color), Brush> Glows = [];

    public RgbColor Color
    {
        get => (RgbColor)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public Brush? OffBrush
    {
        get => (Brush?)GetValue(OffBrushProperty);
        set => SetValue(OffBrushProperty, value);
    }

    public bool IsLit
    {
        get => (bool)GetValue(IsLitProperty);
        set => SetValue(IsLitProperty, value);
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

    // The radius of the glow; outside it the glow has faded out.
    internal double GlowRadius => Diameter / 2.0 + GlowSpread + GlowBlur;

    protected override Size MeasureOverride(Size availableSize) => new(Diameter, Diameter);

    protected override void OnRender(DrawingContext drawingContext)
    {
        var center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
        var radius = Diameter / 2.0;
        if (IsLit)
        {
            drawingContext.DrawEllipse(Glow(), null, center, GlowRadius, GlowRadius);
        }

        drawingContext.DrawEllipse(IsLit ? ColorBrushes.Get(Color) : OffBrush, null, center, radius, radius);
    }

    internal Brush Glow()
    {
        var key = (Diameter, GlowBlur, GlowSpread, Rounded(Color));
        if (Glows.TryGetValue(key, out var glow))
        {
            return glow;
        }

        if (Glows.Count >= MaxCachedGlows)
        {
            Glows.Clear();
        }

        glow = CreateGlow(key.Item4);
        Glows[key] = glow;
        return glow;
    }

    // The top 5 bits, repeated below, so black and white stay black and white.
    internal static RgbColor Rounded(RgbColor color) => new(Round(color.R), Round(color.G), Round(color.B));

    private static byte Round(byte channel) => (byte)((channel & 0xF8) | (channel >> 5));

    // A blur of b around the spread edge fades from about half its color there to nothing b further
    // out, and is about full b inside it.
    private RadialGradientBrush CreateGlow(RgbColor color)
    {
        var outer = GlowRadius;
        var edge = (Diameter / 2.0 + GlowSpread) / outer;
        var blur = GlowBlur / outer;
        var brush = new RadialGradientBrush
        {
            GradientStops =
            {
                new GradientStop(WithAlpha(color, 0xFF), Math.Max(0.0, edge - blur)),
                new GradientStop(WithAlpha(color, 0x80), edge),
                new GradientStop(WithAlpha(color, 0x1C), edge + blur / 2.0),
                new GradientStop(WithAlpha(color, 0x00), 1.0),
            },
        };
        return Freeze(brush);
    }

    private static System.Windows.Media.Color WithAlpha(RgbColor color, byte alpha) => System.Windows.Media.Color.FromArgb(alpha, color.R, color.G, color.B);

    private static T Freeze<T>(T brush)
        where T : Freezable
    {
        brush.Freeze();
        return brush;
    }
}
