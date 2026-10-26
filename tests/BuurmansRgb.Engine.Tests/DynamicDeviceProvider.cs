using System.Collections.Concurrent;
using BuurmansRgb.Core;

namespace BuurmansRgb.Engine.Tests;

// A provider whose devices the test changes, like OpenRGB starting or stopping. A rescan takes
// DevicesAfterRescan, when set.
internal sealed class DynamicDeviceProvider : IRescannableDeviceProvider
{
    private volatile IReadOnlyList<RgbDevice> _devices = [];
    private int _rescanCount;

    public string Name => "Dynamic";

    public event EventHandler? DevicesChanged;

    public ConcurrentDictionary<string, RgbColor[]> Leds { get; } = new();

    public bool HasDevicesChangedListeners => DevicesChanged is not null;

    public static RgbDevice Device(string id, int ledCount = 2) =>
        new(id, $"Device {id}", DeviceType.Other, [RgbZone.Fixed("Zone", ledCount)]) { Source = DeviceSource.Hardware };

    public void SetDevices(params IReadOnlyList<RgbDevice> devices)
    {
        _devices = devices;
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    // When set, DiscoverAsync answers once this task completes, like a slow provider, and
    // DiscoverStarted completes when it starts waiting.
    public Task? DiscoverGate { get; set; }

    public TaskCompletionSource DiscoverStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<IReadOnlyList<RgbDevice>> DiscoverAsync(CancellationToken ct)
    {
        if (DiscoverGate is { } gate)
        {
            DiscoverStarted.TrySetResult();
            await gate;
        }

        return _devices;
    }

    public void SetLeds(string deviceId, ReadOnlySpan<RgbColor> colors) => Leds[deviceId] = colors.ToArray();

    public IReadOnlyList<RgbDevice>? DevicesAfterRescan { get; set; }

    // What a rescan answers: false as if the source of the devices could not be reached.
    public bool RescanReaches { get; set; } = true;

    // When set, a rescan throws this.
    public Exception? RescanException { get; set; }

    public int RescanCount => Volatile.Read(ref _rescanCount);

    public Task<bool> RequestRescanAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _rescanCount);
        if (RescanException is { } exception)
        {
            return Task.FromException<bool>(exception);
        }

        if (DevicesAfterRescan is { } devices)
        {
            SetDevices(devices);
        }

        return Task.FromResult(RescanReaches);
    }
}
