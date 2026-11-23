using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.UI.Tests;

// The full-size US keyboard of Providers.Simulated: 104 keys in a key map 24 cells wide, with the
// key names OpenRGB gives such a keyboard (without its "Key: " prefix). Taken from the provider
// itself, so the UI tests and the simulated keyboard of the app cannot drift apart.
internal static class TestKeyboards
{
    private static readonly RgbZone Keys = new SimulatedDeviceProvider().DiscoverAsync(CancellationToken.None).GetAwaiter().GetResult()
        .Single(device => device.Type == DeviceType.Keyboard).Zones.Single();

    public static readonly LedMatrix KeyMap = Keys.Matrix!;

    public static readonly IReadOnlyList<string> Names = Keys.LedNames!;

    public static DeviceInfo Create(string id = "keyboard", string name = "Toetsenbord") =>
        new(id, name, DeviceType.Keyboard, Names.Count, [RgbZone.Fixed("Keys", Names.Count) with { Matrix = KeyMap, LedNames = Names }])
        {
            Source = DeviceSource.Hardware,
        };
}
