using System.Text.Json;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;

namespace BuurmansRgb.Engine.Sharing;

// Makes the JSON of a shared scene or own effect (SharedFile), as SharedFileReader reads it; the
// rules they share are in SharedFormat. What belongs to this pc stays out: the times, programs
// and role of a scene, and whether it is on.
internal static class SharedFileWriter
{
    // The scene with each device it names (its devices, and those with lighting, colors per LED or a
    // layer of their own) by the type and name it has in devices, the devices here now; a device that is
    // not here now goes by its id only. The own effects it shows that are in effects come along; an
    // own effect that is gone shows static here (LightingController), and so in the file.
    public static byte[] WriteScene(Profile scene, IReadOnlyList<RgbDevice> devices, IReadOnlyList<CustomEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var lighting = scene.Lighting;
        var shown = new List<CustomEffect>();
        List<string> ids =
        [
            .. (scene.DeviceIds ?? [])
                .Concat(devices.Select(device => device.Id).Where(id => lighting.Devices.ContainsKey(id) || lighting.LedColors.ContainsKey(id) || lighting.Overlays.ContainsKey(id)))
                .Concat(lighting.Devices.Keys.Concat(lighting.LedColors.Keys).Concat(lighting.Overlays.Keys).Order(StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal),
        ];

        var file = new SharedFile
        {
            Version = SharedFormat.Version,
            Kind = SharedFormat.SceneKind,
            Name = SharedFiles.ForSharing(scene.Name),
            Lighting = Write(lighting.Global, effects, shown),
            Devices = ids.Count > 0 ? [.. ids.Select(id => WriteDevice(id, lighting, devices, effects, shown))] : null,
            OnlyDevices = scene.DeviceIds is { } only ? [.. only] : null,
        };
        return Serialize(file with { Effects = shown.Count > 0 ? [.. shown.Select(ToShared)] : null });
    }

    public static byte[] WriteEffect(CustomEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return Serialize(new SharedFile { Version = SharedFormat.Version, Kind = SharedFormat.EffectKind, Effects = [ToShared(effect)] });
    }

    private static SharedDevice WriteDevice(string id, LightingState lighting, IReadOnlyList<RgbDevice> devices, IReadOnlyList<CustomEffect> effects, List<CustomEffect> shown)
    {
        var device = devices.FirstOrDefault(known => known.Id == id);
        var name = device?.Name is { } known && SharedFormat.IsValidDeviceName(known) ? known : null;
        var colors = lighting.LedColors.GetValueOrDefault(id);
        return new SharedDevice
        {
            Id = id,
            Type = device is null ? null : SharedFormat.WriteType(device.Type),
            Name = name,
            Lighting = lighting.Devices.TryGetValue(id, out var own) ? Write(own, effects, shown) : null,
            LedColors = colors is null ? null : [.. colors.Colors],
            ZoneLengths = colors?.ZoneLengths is { Count: > 0 } lengths ? [.. lengths] : null,

            // Only with a layer, so a scene without one stays as a version before the layer reads it.
            Overlay = lighting.Overlays.GetValueOrDefault(id) is { } overlay ? [.. overlay] : null,
        };
    }

    // An own effect that is not in effects (any more) goes as static, as the lighting shows it.
    // One that is goes into shown, once.
    private static SharedLighting Write(DeviceLighting lighting, IReadOnlyList<CustomEffect> effects, List<CustomEffect> shown)
    {
        var effectId = lighting.EffectId;
        if (CustomEffects.IsCustom(effectId))
        {
            if (effects.FirstOrDefault(effect => effect.Id == effectId) is { } effect)
            {
                if (!shown.Contains(effect))
                {
                    shown.Add(effect);
                }
            }
            else
            {
                effectId = EffectIds.Static;
            }
        }

        return new SharedLighting
        {
            Color = lighting.Color,
            Brightness = lighting.Brightness,
            Effect = effectId,
            Speed = lighting.Speed.Equals(EffectSpeed.Default) ? null : lighting.Speed,
            Direction = lighting.Direction,
            On = lighting.On ? null : false,
            ColorLoop = lighting.Loop == ColorLoop.Default ? null : new SharedColorLoop { Colors = [.. lighting.Loop.Colors], Spread = lighting.Loop.Spread },
            EffectColors = lighting.EffectColors.Lists.Count == 0
                ? null
                : lighting.EffectColors.Lists.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value.ToList(), StringComparer.Ordinal),
        };
    }

    private static SharedEffect ToShared(CustomEffect effect) => new()
    {
        Id = effect.Id,
        Name = SharedFiles.ForSharing(effect.Name),
        Steps = [.. effect.Steps.Select(step => new SharedStep { Colors = [.. step.Colors], Hold = step.HoldSeconds, Fade = step.FadeSeconds })],
    };

    private static byte[] Serialize(SharedFile file) => JsonSerializer.SerializeToUtf8Bytes(file, SharingJsonContext.Default.SharedFile);
}
