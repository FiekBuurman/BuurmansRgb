using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

internal static class LightingStates
{
    public static LightingState Uniform(DeviceLighting global) => WithDevices(global, new Dictionary<string, DeviceLighting>());

    // Without stored per-LED colors.
    public static LightingState WithDevices(DeviceLighting global, IReadOnlyDictionary<string, DeviceLighting> devices) =>
        new(global, devices, new Dictionary<string, IReadOnlyList<RgbColor?>>());

    public static DeviceLighting For(this LightingState state, string deviceId) => state.Devices.GetValueOrDefault(deviceId, state.Global);
}
