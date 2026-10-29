using System.Windows;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// Where the preview draws the LEDs of a zone in a matrix of cells that each hold one LED, or a
// gap (LedPlaces.Wrap and LedPlaces.Place make one). A fan ring has RingLayout instead.
public static class LedLayout
{
    // The size of an LED and the space between two LEDs at full size.
    public const double LedSize = 14.0;
    public const double Spacing = 4.0;
    public const double Pitch = LedSize + Spacing;

    // A wide key map shrinks to fit, but no further than this, so the keys stay clickable.
    public const double MinScale = 0.5;

    // The distance from one cell to the next when the layout is drawn in width. Only a layout
    // that shrinks, such as a key map, gets a smaller pitch when it is wider than width; dots wrap
    // to the next row instead. An unbounded width, as in a measure pass, keeps the full pitch.
    public static double PitchFor(LedMatrix layout, double width, bool shrinks) =>
        shrinks ? Pitch * ScaleFor(layout.Width * Pitch - Spacing, width) : Pitch;

    // How much a layout of naturalWidth at full size shrinks to fit in width, down to MinScale.
    public static double ScaleFor(double naturalWidth, double width) =>
        !double.IsFinite(width) || naturalWidth <= width ? 1.0 : Math.Max(width / naturalWidth, MinScale);

    // How far from its middle a click still finds an LED: a target of at least 24 px across (WCAG
    // 2.5.8), also for an LED that is drawn smaller, such as a dot of 14 px or a shrunk key of 7.
    public const double HitRadius = 12.0;

    // The LED a click on point picks, of the LEDs drawn in bounds (Rect.Empty for one that is not
    // shown): the one drawn under it, or else the nearest within HitRadius of its middle; NoLed
    // farther away. The middle of a wide key, such as the space bar, is the line through it that
    // ends half its height from its ends, so the whole key counts, and HitRadius around that line.
    // Of two as near, the first in LED order. Allocates nothing, as it runs on every mouse move.
    public static int NearestLed(ReadOnlySpan<Rect> bounds, Point point)
    {
        var nearest = LedMatrix.NoLed;
        var nearestDistance = HitRadius * HitRadius;
        for (var led = 0; led < bounds.Length; led++)
        {
            ref readonly var box = ref bounds[led];
            var (left, top, width, height) = (box.X, box.Y, box.Width, box.Height);

            // Most LEDs are farther than HitRadius from the box; so is every point from Rect.Empty.
            if (box.IsEmpty || point.X < left - HitRadius || point.X > left + width + HitRadius
                || point.Y < top - HitRadius || point.Y > top + height + HitRadius)
            {
                continue;
            }

            if (point.X >= left && point.X <= left + width && point.Y >= top && point.Y <= top + height)
            {
                return led;
            }

            var half = Math.Min(width, height) / 2.0;
            var dx = Math.Max(Math.Max(left + half - point.X, point.X - (left + width - half)), 0.0);
            var dy = Math.Max(Math.Max(top + half - point.Y, point.Y - (top + height - half)), 0.0);
            var distance = dx * dx + dy * dy;
            if (distance < nearestDistance || (distance == nearestDistance && nearest == LedMatrix.NoLed))
            {
                nearest = led;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    // How many cells from this one on in its row hold the same LED: the width of a wide key.
    public static int SpanAt(LedMatrix layout, int cell)
    {
        var led = layout.Cells[cell];
        var rowEnd = (cell / layout.Width + 1) * layout.Width;
        var span = 1;
        while (cell + span < rowEnd && layout.Cells[cell + span] == led)
        {
            span++;
        }

        return span;
    }
}
