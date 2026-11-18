using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;

namespace BuurmansRgb.Engine.Sharing;

// Sharing scenes and own effects (IEngineApi.ExportSceneAsync, ExportCustomEffectAsync and
// ImportSharedAsync): the file of SharedFileWriter out, and one from someone else, once
// SharedFileReader accepted it, in as a new scene or own effect. The devices of a scene become
// those here (SharedDeviceMatcher); a device that is not here is left out of its lighting and
// devices, and named in the result. Its own effects come first (CustomEffectLibrary.Import), so the
// scene shows them by their ids here; the scene is added within that import, so when it cannot be
// saved the effects go again and nothing is added. Without the library of own effects (in tests) a
// scene with one, or an own effect, is refused as TooManyEffects, as none can be kept.
internal sealed class SceneSharing(ProfileLibrary profiles, LightingController lighting, CustomEffectLibrary? customEffects)
{
    public byte[] ExportScene(string name) =>
        Importable(SharedFileWriter.WriteScene(profiles.Get(name), lighting.Devices, customEffects?.Effects ?? []));

    public byte[] ExportEffect(string id) =>
        Importable(SharedFileWriter.WriteEffect(
            (customEffects?.Effects ?? []).FirstOrDefault(effect => effect.Id == id)
            ?? throw new ArgumentException($"There is no own effect '{id}'.", nameof(id))));

    public ShareImportResult Import(byte[] json)
    {
        var content = SharedFileReader.Read(json);
        return content switch
        {
            { Outcome: not ShareImportOutcome.Imported } => ShareImportResult.Refused(content.Outcome),
            { Scene: { } scene } => ImportScene(scene),
            _ => ImportEffect(content.Effect!),
        };
    }

    private ShareImportResult ImportEffect(CustomEffect effect)
    {
        if (customEffects?.Import([effect]) is not [var (saved, added)])
        {
            return ShareImportResult.Refused(ShareImportOutcome.TooManyEffects);
        }

        Trace.TraceInformation(added ? $"Imported the own effect '{effect.Name}' as '{saved.Name}'." : $"The own effect '{effect.Name}' was here already.");
        return new ShareImportResult(ShareImportOutcome.Imported, SharedKind.Effect, saved.Name, effect.Name, AlreadyThere: !added);
    }

    private ShareImportResult ImportScene(SharedScene scene)
    {
        var (map, unmatched) = SharedDeviceMatcher.Match(scene.Devices, lighting.Devices);
        List<string>? onlyDevices = scene.OnlyDevices is { } only ? [.. only.Where(map.ContainsKey).Select(id => map[id])] : null;
        if (onlyDevices is { Count: 0 })
        {
            Trace.TraceWarning($"Import: none of the devices of the scene '{scene.Name}' is here.");
            return ShareImportResult.Refused(ShareImportOutcome.NoDevices);
        }

        // Only the own effects that the lighting of all devices, or of a device here, still shows.
        var shown = new[] { scene.Lighting.Global }
            .Concat(scene.Lighting.Devices.Where(pair => map.ContainsKey(pair.Key)).Select(pair => pair.Value))
            .Select(device => device.EffectId)
            .ToHashSet(StringComparer.Ordinal);
        List<CustomEffect> sharedEffects = [.. scene.Effects.Where(effect => shown.Contains(effect.Id))];

        // A profiles.json that does not read fails here, before an own effect is added.
        _ = profiles.Names;
        var effectIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var addedEffects = new List<string>();
        string? name = null;
        if (sharedEffects.Count == 0)
        {
            name = AddScene(scene, map, effectIds, onlyDevices);
        }
        else if (customEffects?.Import(sharedEffects, effects =>
        {
            for (var i = 0; i < effects.Count; i++)
            {
                effectIds[sharedEffects[i].Id] = effects[i].Effect.Id;
                if (effects[i].Added)
                {
                    addedEffects.Add(effects[i].Effect.Name);
                }
            }

            // When this throws, the library takes the effects it added out again.
            name = AddScene(scene, map, effectIds, onlyDevices);
        }) is null)
        {
            return ShareImportResult.Refused(ShareImportOutcome.TooManyEffects);
        }

        // A device without a name is only counted: its id means nothing to the user.
        List<string> notHere = [.. unmatched.Select(device => device.Name).OfType<string>()];
        var unnamed = unmatched.Count(device => device.Name is null);
        Trace.TraceInformation($"Imported the scene '{scene.Name}' as '{name!}'; {map.Count} devices here, {notHere.Count + unnamed} not.");
        return new ShareImportResult(
            ShareImportOutcome.Imported, SharedKind.Scene, name!, scene.Name, UnmatchedDevices: notHere, AddedEffects: addedEffects, UnnamedUnmatchedDevices: unnamed);
    }

    // A file goes out only when another pc would take it: one beyond the limits of SharedFileReader,
    // such as a scene that still has the lighting of more than SharedFiles.MaxDevices devices, some
    // long gone, or larger than SharedFiles.MaxJsonBytes, throws instead. The reader traced why.
    private static byte[] Importable(byte[] json)
    {
        var outcome = SharedFileReader.Read(json).Outcome;
        return outcome == ShareImportOutcome.Imported
            ? json
            : throw new InvalidOperationException($"The file would not import on another pc ({outcome}); see the warning before this.");
    }
    // The scene with the devices and own effects of this pc.
    private string AddScene(SharedScene scene, IReadOnlyDictionary<string, string> map, Dictionary<string, string> effectIds, List<string>? onlyDevices)
    {
        var state = scene.Lighting;
        var shared = new LightingState(
            WithEffect(state.Global, effectIds),
            state.Devices.Where(pair => map.ContainsKey(pair.Key)).ToDictionary(pair => map[pair.Key], pair => WithEffect(pair.Value, effectIds)),
            state.LedColors.Where(pair => map.ContainsKey(pair.Key)).ToDictionary(pair => map[pair.Key], pair => pair.Value));
        return profiles.Add(scene.Name, shared, onlyDevices);
    }

    private static DeviceLighting WithEffect(DeviceLighting lighting, Dictionary<string, string> effectIds) =>
        effectIds.TryGetValue(lighting.EffectId, out var id) ? lighting with { EffectId = id } : lighting;
}
