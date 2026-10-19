using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Tests;

internal sealed class RecordingDeviceProvider : IDeviceProvider
{
    public const string DeviceId = "recording-device";

    private static readonly IReadOnlyList<RgbDevice> Devices =
        [new(DeviceId, "Recording device", DeviceType.Other, [new RgbZone("Zone", 3, Resizable: false)])];

    public string Name => "Recording";

    public int SetLedsCount { get; private set; }

    public RgbColor[] LastColors { get; private set; } = [];

    public Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct) => Task.FromResult(Devices);

    public void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors)
    {
        SetLedsCount++;
        LastColors = colors.ToArray();
    }
}
