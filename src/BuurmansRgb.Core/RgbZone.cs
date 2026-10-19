namespace BuurmansRgb.Core;

// A zone holds MinLedCount to MaxLedCount LEDs. When those differ its length can be set, like an
// addressable header that cannot know how long the strip on it is. A zone with a Matrix, such as
// the keys of a keyboard, also tells where its LEDs sit. An empty map is no Matrix. A cell may
// point past LedCount, as a key map from a device can; whoever reads the LEDs through the matrix
// treats such a cell as a gap, so providers pass the map on as they get it.
public sealed record RgbZone(string Name, int LedCount, int MinLedCount, int MaxLedCount)
{
    public LedMatrix? Matrix { get; init; }

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
