using System.Windows;

namespace BuurmansRgb.UI.Views;

// The LEDs of a fan as a ring of dots (see RingLayout), drawn at scale.
internal sealed class RingArrangement : ILedArrangement
{
    private readonly Rect[] _bounds;

    public RingArrangement(int ledCount, double scale)
    {
        LedCount = ledCount;
        Scale = scale;
        var side = RingLayout.Size(ledCount) * scale;
        Size = new(side, side);
        var size = LedLayout.LedSize * scale;
        _bounds = new Rect[ledCount];
        for (var led = 0; led < ledCount; led++)
        {
            var center = RingLayout.Center(led, ledCount);
            _bounds[led] = new Rect(center.X * scale - size / 2.0, center.Y * scale - size / 2.0, size, size);
        }
    }

    public int LedCount { get; }

    public Size Size { get; }

    public double Scale { get; }

    public bool Keys => false;

    public IReadOnlyList<Rect> Bounds => _bounds;

    public int LedAt(Point point) => RingLayout.LedAt(LedCount, Scale, point.X, point.Y);
}
