using System.Text.Json.Serialization;

namespace BuurmansRgb.Contracts;

// "Uit om" and "Aan om" in the block Hele huis on Apparaten: when every device goes off, and when
// they come back on, each time on its own days; no times leave that one off. Off are the times of
// the scene OffScene, the start scene "Alles uit" (also after the user renamed it), the same times
// as on Scènes; without that scene, null, of an internal profile of the engine. Two are equal when
// they have the same times in the same order.
public sealed record WholeHouseTimes
{
    [JsonConstructor]
    public WholeHouseTimes(IReadOnlyList<LoadTime>? Off = null, IReadOnlyList<LoadTime>? On = null, string? OffScene = null)
    {
        this.Off = Off is null ? [] : [.. Off];
        this.On = On is null ? [] : [.. On];
        this.OffScene = OffScene;
    }

    public static WholeHouseTimes None { get; } = new();

    public IReadOnlyList<LoadTime> Off { get; init; }

    public IReadOnlyList<LoadTime> On { get; init; }

    public string? OffScene { get; init; }

    public bool Equals(WholeHouseTimes? other) =>
        other is not null && Off.SequenceEqual(other.Off) && On.SequenceEqual(other.On) && OffScene == other.OffScene;

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var time in Off)
        {
            hash.Add(time);
        }

        hash.Add(On.Count);
        foreach (var time in On)
        {
            hash.Add(time);
        }

        hash.Add(OffScene);
        return hash.ToHashCode();
    }
}
