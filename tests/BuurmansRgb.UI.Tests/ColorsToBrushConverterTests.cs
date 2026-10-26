using System.Globalization;
using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

public sealed class ColorsToBrushConverterTests
{
    private static readonly ColorsToBrushConverter Converter = new();

    [Fact]
    public void Convert_OneColor_IsThatColor()
    {
        var brush = Assert.IsType<SolidColorBrush>(Convert([Orange]));

        Assert.Equal(ColorBrushes.ToMediaColor(Orange), brush.Color);
    }

    // From the first color on the left to the last on the right.
    [Fact]
    public void Convert_Pattern_IsAGradientFromLeftToRight()
    {
        var brush = Assert.IsType<LinearGradientBrush>(Convert([Red, Green, Blue]));

        Assert.Equal([ColorBrushes.ToMediaColor(Red), ColorBrushes.ToMediaColor(Green), ColorBrushes.ToMediaColor(Blue)], brush.GradientStops.Select(stop => stop.Color));
        Assert.Equal([0.0, 0.5, 1.0], brush.GradientStops.Select(stop => stop.Offset));
        Assert.True(brush.IsFrozen);
    }

    [Fact]
    public void Convert_LongPattern_TakesAtMostTheMostStopsWithBothEnds()
    {
        RgbColor[] colors = [.. Enumerable.Range(0, 104).Select(i => new RgbColor((byte)i, 0, 0))];

        var brush = Assert.IsType<LinearGradientBrush>(Convert(colors));

        Assert.Equal(ColorsToBrushConverter.MaxStops, brush.GradientStops.Count);
        Assert.Equal(ColorBrushes.ToMediaColor(colors[0]), brush.GradientStops[0].Color);
        Assert.Equal(ColorBrushes.ToMediaColor(colors[^1]), brush.GradientStops[^1].Color);
    }

    [Fact]
    public void Convert_NoColors_IsTransparent()
    {
        Assert.Same(Brushes.Transparent, Convert([]));
    }

    private static object Convert(IReadOnlyList<RgbColor> colors) =>
        Converter.Convert(colors, typeof(Brush), null!, CultureInfo.InvariantCulture);
}
