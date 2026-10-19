using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

internal static class LightingStates
{
    public static LightingState Uniform(DeviceLighting global) => new(global, new Dictionary<string, DeviceLighting>());

    public static DeviceLighting For(this LightingState state, string deviceId) => state.Devices.GetValueOrDefault(deviceId, state.Global);
}
