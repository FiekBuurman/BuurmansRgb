using System.Text.Json.Serialization;

namespace BuurmansRgb.Core;

// A zone holds MinLedCount to MaxLedCount LEDs. When those differ its length can be set, like an
// addressable header that cannot know how long the strip on it is. A zone with a Matrix, such as
// the keys of a keyboard, also tells where its LEDs sit. An empty map is no Matrix. A cell may
// point past LedCount, as a key map from a device can; whoever reads the LEDs through the matrix
// treats such a cell as a gap, so providers pass the map on as they get it. Two zones are equal
// when their matrices and LED names are equal one by one, not only when they share the same list.
public sealed record RgbZone(string Name, int LedCount, int MinLedCount, int MaxLedCount)
{
    public LedMatrix? Matrix { get; init; }

    // The name of each LED in zone order, such as "Escape" for a key; null when the provider has
    // none. Like a key map, the list may hold fewer or more names than LEDs. Left out of the JSON
    // when null, so a zone without names costs nothing extra on the pipe.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? LedNames { get; init; }

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

    public bool Equals(RgbZone? other) =>
        other is not null
        && Name == other.Name
        && LedCount == other.LedCount
        && MinLedCount == other.MinLedCount
        && MaxLedCount == other.MaxLedCount
        && Equals(Matrix, other.Matrix)
        && (LedNames is null ? other.LedNames is null : other.LedNames is not null && LedNames.SequenceEqual(other.LedNames));

    public override int GetHashCode() => HashCode.Combine(Name, LedCount, MinLedCount, MaxLedCount, Matrix, LedNames?.Count);
}
