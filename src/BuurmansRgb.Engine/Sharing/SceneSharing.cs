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
// scene shows them by their ids here. Without the library of own effects (in tests) a scene with
// one, or an own effect, is refused as TooManyEffects, as none can be kept.
internal sealed class SceneSharing(ProfileLibrary profiles, LightingController lighting, CustomEffectLibrary? customEffects)
{
    public byte[] ExportScene(string name) =>
        SharedFileWriter.WriteScene(profiles.Get(name), lighting.Devices, customEffects?.Effects ?? []);

    public byte[] ExportEffect(string id) =>
        SharedFileWriter.WriteEffect(
            (customEffects?.Effects ?? []).FirstOrDefault(effect => effect.Id == id)
            ?? throw new ArgumentException($"There is no own effect '{id}'.", nameof(id)));

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

        var effectIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var addedEffects = new List<string>();
        if (scene.Effects.Count > 0)
        {
            if (customEffects?.Import(scene.Effects) is not { } effects)
            {
                return ShareImportResult.Refused(ShareImportOutcome.TooManyEffects);
            }

            for (var i = 0; i < effects.Count; i++)
            {
                effectIds[scene.Effects[i].Id] = effects[i].Effect.Id;
                if (effects[i].Added)
                {
                    addedEffects.Add(effects[i].Effect.Name);
                }
            }
        }

        var state = scene.Lighting;
        var shared = new LightingState(
            WithEffect(state.Global, effectIds),
            state.Devices.Where(pair => map.ContainsKey(pair.Key)).ToDictionary(pair => map[pair.Key], pair => WithEffect(pair.Value, effectIds)),
            state.LedColors.Where(pair => map.ContainsKey(pair.Key)).ToDictionary(pair => map[pair.Key], pair => pair.Value));
        var name = profiles.Add(scene.Name, shared, onlyDevices);
        List<string> notHere = [.. unmatched.Select(device => device.Name ?? device.Id)];
        Trace.TraceInformation($"Imported the scene '{scene.Name}' as '{name}'; {map.Count} devices here, {notHere.Count} not.");
        return new ShareImportResult(ShareImportOutcome.Imported, SharedKind.Scene, name, scene.Name, UnmatchedDevices: notHere, AddedEffects: addedEffects);
    }

    private static DeviceLighting WithEffect(DeviceLighting lighting, Dictionary<string, string> effectIds) =>
        effectIds.TryGetValue(lighting.EffectId, out var id) ? lighting with { EffectId = id } : lighting;
}
