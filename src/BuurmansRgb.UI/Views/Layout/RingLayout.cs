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
}
