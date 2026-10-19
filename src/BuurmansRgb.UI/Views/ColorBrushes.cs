using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// Frozen brushes, cached per color: a preview redraws the same few colors over and over.
public static class ColorBrushes
{
    internal const int MaxCached = 4096;

    private static readonly Dictionary<RgbColor, SolidColorBrush> Cache = [];

    // Only used on the UI thread.
    public static SolidColorBrush Get(RgbColor color)
    {
        if (Cache.TryGetValue(color, out var brush))
        {
            return brush;
        }

        if (Cache.Count >= MaxCached)
        {
            Cache.Clear();
        }

        brush = new SolidColorBrush(ToMediaColor(color));
        brush.Freeze();
        Cache[color] = brush;
        return brush;
    }

    public static Color ToMediaColor(RgbColor color) => System.Windows.Media.Color.FromRgb(color.R, color.G, color.B);
}
