using System.Text.Json.Serialization;

namespace BuurmansRgb.Contracts;

// An own effect, made by the user: a name and the steps it runs through, after the last one the
// first again. Id is its effect id (CustomEffects.IsCustom), which the lighting and the scenes use
// like the id of any other effect, so a new name changes nothing there. Two effects are equal when
// they have the same id, name and steps, in the same order.
public sealed record CustomEffect
{
    [JsonConstructor]
    public CustomEffect(string Id, string Name, IReadOnlyList<CustomEffectStep> Steps)
    {
        this.Id = Id;
        this.Name = Name;
        this.Steps = Steps is null ? [] : [.. Steps];
    }

    public string Id { get; init; }

    public string Name { get; init; }

    public IReadOnlyList<CustomEffectStep> Steps { get; init; }

    // Whether it moves: more than one step, and some time to show them in.
    [JsonIgnore]
    public bool IsAnimated => Steps.Count > 1 && Steps.Any(step => step.HoldSeconds + step.FadeSeconds > 0.0);

    public bool Equals(CustomEffect? other) =>
        other is not null
        && Id == other.Id
        && Name == other.Name
        && Steps.SequenceEqual(other.Steps);

    public override int GetHashCode() => HashCode.Combine(Id, Name, Steps.Count);
}
