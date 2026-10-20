namespace BuurmansRgb.Core;

// A zone holds MinLedCount to MaxLedCount LEDs. When those differ its length can be set, like an
// addressable header that cannot know how long the strip on it is.
public sealed record RgbZone(string Name, int LedCount, int MinLedCount, int MaxLedCount)
{
    public bool Resizable => MinLedCount < MaxLedCount;

    // A zone whose length cannot change.
    public static RgbZone Fixed(string name, int ledCount) => new(name, ledCount, ledCount, ledCount);

    public bool CanHold(int ledCount) => ledCount >= MinLedCount && ledCount <= MaxLedCount;

    public void ThrowIfCannotHold(int ledCount)
    {
        if (!CanHold(ledCount))
        {
            throw new ArgumentOutOfRangeException(nameof(ledCount), ledCount,
                $"Zone '{Name}' holds {MinLedCount} to {MaxLedCount} LEDs.");
        }
    }
}
