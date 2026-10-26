using System.Windows.Media;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// Frozen brushes, cached per color: a preview redraws the same few colors over and over.
public static class ColorBrushes
{
    internal const int MaxCached = 4096;

    // One cache per UI thread: the brushes are frozen, so any thread may use them, but the
    // dictionary is not thread-safe. Tests run windows on several threads at once.
    [ThreadStatic]
    private static Dictionary<RgbColor, SolidColorBrush>? _cache;

    private static Dictionary<RgbColor, SolidColorBrush> Cache => _cache ??= [];

    public static SolidColorBrush Get(RgbColor color)
    {
        var cache = Cache;
        if (cache.TryGetValue(color, out var brush))
        {
            return brush;
        }

        if (cache.Count >= MaxCached)
        {
            cache.Clear();
        }

        brush = new SolidColorBrush(ToMediaColor(color));
        brush.Freeze();
        cache[color] = brush;
        return brush;
    }

    public static Color ToMediaColor(RgbColor color) => System.Windows.Media.Color.FromRgb(color.R, color.G, color.B);
}
