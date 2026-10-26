using System.Diagnostics;
using System.Text.Json;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Settings;

// effects.json next to settings.json, as profiles.json is. A file that cannot be read throws, so
// saving never overwrites effects that are only out of reach for a moment.
internal sealed class JsonCustomEffectStore(string filePath) : ICustomEffectStore
{
    public static string DefaultFilePath { get; } = Path.Combine(
        Path.GetDirectoryName(JsonSettingsStore.DefaultFilePath)!,
        "effects.json");

    // Where a file that is not valid JSON goes, so the next save does not overwrite it.
    public string BrokenFilePath => filePath + ".broken";

    // An effect that does not read, or with the id or the name of an earlier one, is skipped, and
    // so are the effects after the first CustomEffects.MaxEffects.
    public IReadOnlyList<CustomEffect> Load()
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        CustomEffectsFile? file;
        try
        {
            file = LenientJson.Deserialize(File.ReadAllBytes(filePath), EngineSettingsJsonContext.Default.CustomEffectsFile);
        }
        catch (JsonException exception)
        {
            Trace.TraceError(exception.ToString());
            File.Move(filePath, BrokenFilePath, overwrite: true);
            return [];
        }

        var effects = new List<CustomEffect>();
        foreach (var settings in file?.Effects ?? [])
        {
            if (settings?.ToCustomEffect() is not { } effect)
            {
                continue;
            }

            if (effects.Exists(other => other.Id == effect.Id || CustomEffects.NameComparer.Equals(other.Name, effect.Name)))
            {
                Trace.TraceWarning($"effects.json: the effect '{effect.Name}' has the id or the name of an earlier one; it is left out.");
                continue;
            }

            if (effects.Count == CustomEffects.MaxEffects)
            {
                Trace.TraceWarning($"effects.json: more than {CustomEffects.MaxEffects} effects; the rest is left out.");
                break;
            }

            effects.Add(effect);
        }

        return effects;
    }

    public void Save(IReadOnlyList<CustomEffect> effects) =>
        JsonFile.Write(filePath, new CustomEffectsFile { Effects = [.. effects.Select(CustomEffectSettings.From)] }, EngineSettingsJsonContext.Default.CustomEffectsFile);
}
