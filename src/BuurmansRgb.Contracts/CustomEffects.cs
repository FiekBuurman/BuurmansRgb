using System.Globalization;

namespace BuurmansRgb.Contracts;

// The rules for own effects (CustomEffect), shared so the settings app can check before it asks.
// The engine keeps them in effects.json.
public static class CustomEffects
{
    // The start of the effect id of every own effect, such as "custom:1a2b3c4d".
    public const string IdPrefix = "custom:";

    public const int MaxEffects = 32;

    public const int MinSteps = 1;

    public const int MaxSteps = 32;

    // The most colors of a pattern: more LEDs than a device has, by far.
    public const int MaxLeds = 1024;

    public const double MinSeconds = 0.0;

    public const double MaxSeconds = 60.0;

    public const double DefaultHoldSeconds = 1.0;

    public const double DefaultFadeSeconds = 1.0;

    public const int MaxNameLength = 40;

    // Names match without regard to case, so "Disco" and "disco" are the same effect.
    public static StringComparer NameComparer { get; } = StringComparer.CurrentCultureIgnoreCase;

    // Whether the effect id is that of an own effect: the prefix and then letters, digits or '-'.
    public static bool IsCustom(string? effectId) =>
        effectId is not null
        && effectId.Length > IdPrefix.Length
        && effectId.Length <= IdPrefix.Length + 32
        && effectId.StartsWith(IdPrefix, StringComparison.Ordinal)
        && effectId.AsSpan(IdPrefix.Length).IndexOfAnyExcept("abcdefghijklmnopqrstuvwxyz0123456789-") < 0;

    // A new effect id: the prefix and eight random hex digits.
    public static string NewId() => IdPrefix + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8];

    // The name without surrounding spaces, or null when it is empty, too long or holds a control
    // character such as a line break.
    public static string? NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxNameLength || trimmed.Any(char.IsControl) ? null : trimmed;
    }

    // The first of the names numbered 1, 2, ... that is not taken, such as "Eigen effect 3". There
    // are at most MaxEffects effects, so one of the first MaxEffects + 1 is free.
    public static string FirstFreeName(Func<int, string> numbered, Func<string, bool> isTaken)
    {
        ArgumentNullException.ThrowIfNull(numbered);
        ArgumentNullException.ThrowIfNull(isTaken);
        return Enumerable.Range(1, MaxEffects + 1).Select(numbered).First(name => !isTaken(name));
    }

    public static bool IsValidSeconds(double seconds) => seconds is >= MinSeconds and <= MaxSeconds;

    // The effect as the engine keeps it: a valid id and name (NormalizeName) and MinSteps to
    // MaxSteps valid steps. Throws ArgumentException with what is wrong.
    public static CustomEffect Normalize(CustomEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if (!IsCustom(effect.Id))
        {
            throw new ArgumentException($"'{effect.Id}' is not the id of an own effect.", nameof(effect));
        }

        var name = NormalizeName(effect.Name) ?? throw new ArgumentException($"'{effect.Name}' is not a valid name for an effect.", nameof(effect));
        if (effect.Steps.Count is < MinSteps or > MaxSteps)
        {
            throw new ArgumentException($"An own effect has {MinSteps} to {MaxSteps} steps, not {effect.Steps.Count}.", nameof(effect));
        }

        if (effect.Steps.Any(step => step is null || !step.IsValid))
        {
            throw new ArgumentException(
                $"A step has 1 to {MaxLeds} colors and stays and fades {MinSeconds} to {MaxSeconds} seconds.", nameof(effect));
        }

        return effect with { Name = name };
    }
}
