using System.Windows;
using BuurmansRgb.Core;

namespace BuurmansRgb.UI.Views;

// Where the preview draws the LEDs of a fan ring, at full size from the top left of the ring, on
// the circle of LedPlaces.OnRing.
public static class RingLayout
{
    // The distance from the middle of the ring to the middle of its LEDs. Two neighbours are a
    // pitch apart, as in a row, but the ring is never smaller than two pitches across, so a ring
    // of three LEDs still looks like a ring. A single LED sits in the middle.
    public static double Radius(int ledCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ledCount);
        return ledCount <= 1 ? 0.0 : Math.Max(LedLayout.Pitch / (2.0 * Math.Sin(Math.PI / ledCount)), LedLayout.Pitch);
    }

    // The width and height of a ring of ledCount LEDs.
    public static double Size(int ledCount) => ledCount == 0 ? 0.0 : 2.0 * Radius(ledCount) + LedLayout.LedSize;

    // The middle of an LED, where LedPlaces.OnRing puts it, so the engine and the preview agree.
    public static Point Center(int led, int ledCount)
    {
        var (right, down) = LedPlaces.OnRing(led, ledCount);
        var radius = Radius(ledCount);
        var middle = radius + LedLayout.LedSize / 2.0;
        return new(middle + radius * right, middle + radius * down);
    }

    // The LED under a point of a ring drawn at scale; NoLed outside the LEDs. Like a cell of a
    // matrix, an LED counts up to half a pitch from its middle, so a click just beside a dot
    // still picks it. Neighbours are at least a pitch apart, so these circles never overlap, and
    // the LED nearest in angle is the only one to check.
    public static int LedAt(int ledCount, double scale, double x, double y)
    {
        if (ledCount == 0)
        {
            return LedMatrix.NoLed;
        }

        var point = new Point(x / scale, y / scale);
        var middle = Radius(ledCount) + LedLayout.LedSize / 2.0;
        var turn = Math.Atan2(point.X - middle, middle - point.Y) / (2.0 * Math.PI);
        var led = (int)Math.Round((turn < 0 ? turn + 1.0 : turn) * ledCount) % ledCount;
        return (point - Center(led, ledCount)).Length <= LedLayout.Pitch / 2.0 ? led : LedMatrix.NoLed;
    }
}
