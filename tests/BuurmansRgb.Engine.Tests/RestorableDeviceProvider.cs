using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Tests;

// One device of two LEDs that logs what the engine asks, in order: "restore" or "leds".
internal sealed class RestorableDeviceProvider : IRestorableDeviceProvider
{
    public const string DeviceId = "restorable-device";

    private static readonly IReadOnlyList<RgbDevice> Devices =
        [new(DeviceId, "Restorable device", DeviceType.Motherboard, [new RgbZone("Mainboard", 2, Resizable: false)])];

    public List<string> Calls { get; } = [];

    public bool ThrowOnRestore { get; set; }

    public string Name => "Restorable";

    public Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct) => Task.FromResult(Devices);

    public void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors) => Calls.Add("leds");

    public void RestoreDevices()
    {
        Calls.Add("restore");
        if (ThrowOnRestore)
        {
            throw new InvalidOperationException("Restoring failed.");
        }
    }
}
