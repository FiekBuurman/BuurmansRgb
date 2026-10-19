using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Tests;

// One device of three LEDs. SetLeds may come from the render loop's thread.
internal sealed class RecordingDeviceProvider(string deviceId = RecordingDeviceProvider.DeviceId) : IDeviceProvider
{
    public const string DeviceId = "recording-device";

    private readonly IReadOnlyList<RgbDevice> _devices =
        [new(deviceId, "Recording device", DeviceType.Other, [new RgbZone("Zone", 3, Resizable: false)])];

    private readonly Lock _gate = new();
    private int _setLedsCount;
    private RgbColor[] _lastColors = [];

    public string Name => "Recording";

    public int SetLedsCount
    {
        get
        {
            lock (_gate)
            {
                return _setLedsCount;
            }
        }
    }

    public RgbColor[] LastColors
    {
        get
        {
            lock (_gate)
            {
                return _lastColors;
            }
        }
    }

    public Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct) => Task.FromResult(_devices);

    public void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors)
    {
        lock (_gate)
        {
            _setLedsCount++;
            _lastColors = colors.ToArray();
        }
    }
}
