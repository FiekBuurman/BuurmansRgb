using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

internal sealed class InMemoryCustomEffectStore : ICustomEffectStore
{
    public IReadOnlyList<CustomEffect> Effects { get; set; } = [];

    public Exception? LoadException { get; set; }

    public Exception? SaveException { get; set; }

    public int LoadCount { get; private set; }

    // The writes that went through.
    public int SaveCount { get; private set; }

    public IReadOnlyList<CustomEffect> Load()
    {
        LoadCount++;
        if (LoadException is not null)
        {
            throw LoadException;
        }

        return Effects;
    }

    // As JsonCustomEffectStore reads it: an effect that does not read is left out.
    public void Replace(byte[] json)
    {
        var file = LenientJson.Deserialize(json, EngineSettingsJsonContext.Default.CustomEffectsFile);
        Effects = [.. (file?.Effects ?? []).Select(settings => settings?.ToCustomEffect()).OfType<CustomEffect>()];
    }

    public void Delete() => Effects = [];

    public void Save(IReadOnlyList<CustomEffect> effects)
    {
        if (SaveException is not null)
        {
            throw SaveException;
        }

        Effects = [.. effects];
        SaveCount++;
    }
}
