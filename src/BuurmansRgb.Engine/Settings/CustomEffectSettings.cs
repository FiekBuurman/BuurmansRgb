using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Settings;

// One own effect in effects.json: its "id" (such as "custom:1a2b3c4d"), "name" and "steps". Read
// softly, like settings.json: a step that does not read is left out, and an effect without a valid
// id, name or any step is left out whole; the trace says which, for whoever edited the file.
internal sealed record CustomEffectSettings
{
    public JsonText? Id { get; set; }

    public JsonText? Name { get; set; }

    // A step that is not an object reads as null and is left out.
    public List<CustomEffectStepSettings?>? Steps { get; set; }

    public static CustomEffectSettings From(CustomEffect effect) => new()
    {
        Id = JsonText.From(effect.Id),
        Name = JsonText.From(effect.Name),
        Steps = [.. effect.Steps.Select(CustomEffectStepSettings.From)],
    };

    // Null when it cannot be an own effect. More than CustomEffects.MaxSteps steps keeps the first.
    public CustomEffect? ToCustomEffect()
    {
        var id = Id is { IsText: true } idText && CustomEffects.IsCustom(idText.Value) ? idText.Value : null;
        var name = Name is { IsText: true } nameText ? CustomEffects.NormalizeName(nameText.Value) : null;
        CustomEffectStep[] steps = [.. (Steps ?? []).Select(step => step?.ToStep()).OfType<CustomEffectStep>().Take(CustomEffects.MaxSteps)];
        if (id is null || name is null || steps.Length == 0)
        {
            Trace.TraceWarning($"effects.json: the effect '{Name?.Value ?? Id?.Value}' has no valid id, name or step; it is left out.");
            return null;
        }

        return new CustomEffect(id, name, steps);
    }
}

// One step: "colors", one for the whole device or more for a pattern over its LEDs, "hold" and
// "fade" in seconds. A color that does not read is left out of the list; a time that is missing
// takes its default, and one outside 0 to 60 seconds is kept within it.
internal sealed record CustomEffectStepSettings
{
    public RgbColor?[]? Colors { get; set; }

    public double? Hold { get; set; }

    public double? Fade { get; set; }

    public static CustomEffectStepSettings From(CustomEffectStep step) => new()
    {
        Colors = [.. step.Colors.Select(color => (RgbColor?)color)],
        Hold = step.HoldSeconds,
        Fade = step.FadeSeconds,
    };

    // Null without colors; more than CustomEffects.MaxLeds keeps the first.
    public CustomEffectStep? ToStep()
    {
        RgbColor[] colors = [.. (Colors ?? []).OfType<RgbColor>().Take(CustomEffects.MaxLeds)];
        return colors.Length == 0
            ? null
            : new CustomEffectStep(colors, ReadSeconds(Hold, CustomEffects.DefaultHoldSeconds), ReadSeconds(Fade, CustomEffects.DefaultFadeSeconds));
    }

    private static double ReadSeconds(double? seconds, double fallback) =>
        seconds is { } value && double.IsFinite(value) ? Math.Clamp(value, CustomEffects.MinSeconds, CustomEffects.MaxSeconds) : fallback;
}

// The root of effects.json.
internal sealed record CustomEffectsFile
{
    // An effect that is not an object reads as null and is left out.
    public List<CustomEffectSettings?>? Effects { get; set; }
}
