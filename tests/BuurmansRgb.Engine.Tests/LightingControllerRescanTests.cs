using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

// "Verversen" on Apparaten: every provider that can searches again.
public sealed class LightingControllerRescanTests : IDisposable
{
    private readonly DynamicDeviceProvider _dynamic = new();
    private readonly RecordingDeviceProvider _recording = new();
    private readonly LightingController _controller;

    public LightingControllerRescanTests() => _controller = new LightingController([_recording, _dynamic]);

    public void Dispose() => _controller.Dispose();

    // The headset that was switched off and on: once the rescan is done it is in the list again,
    // with its own lighting, and DevicesChanged has gone out.
    [Fact]
    public async Task Rescan_DeviceThatCameBack_IsListedWithItsOwnLighting()
    {
        _dynamic.SetDevices(DynamicDeviceProvider.Device("strip"));
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(WithDevices(StaticLighting(Orange), new Dictionary<string, DeviceLighting> { ["headset"] = StaticLighting(Green) }));
        _dynamic.DevicesAfterRescan = [DynamicDeviceProvider.Device("strip"), DynamicDeviceProvider.Device("headset")];
        var devicesChanged = 0;
        _controller.DevicesChanged += (_, _) => Interlocked.Increment(ref devicesChanged);

        Assert.Equal(RescanOutcome.Rescanned, await _controller.RescanDevicesAsync(Ct));

        Assert.Contains(_controller.Devices, device => device.Id == "headset");
        Assert.Equal([Green, Green], _dynamic.Leds["headset"]);
        Assert.True(Volatile.Read(ref devicesChanged) >= 1);
        Assert.Equal(1, _dynamic.RescanCount);
    }

    [Fact]
    public async Task Rescan_SourceNotReached_IsUnreachable()
    {
        await _controller.DiscoverAsync(Ct);
        _dynamic.RescanOutcome = RescanOutcome.Unreachable;

        Assert.Equal(RescanOutcome.Unreachable, await _controller.RescanDevicesAsync(Ct));
    }

    [Fact]
    public async Task Rescan_ProviderFails_IsUnreachableAndTheOthersStillSearch()
    {
        var other = new DynamicDeviceProvider { DevicesAfterRescan = [DynamicDeviceProvider.Device("fan")] };
        using var controller = new LightingController([_dynamic, other]);
        await controller.DiscoverAsync(Ct);
        _dynamic.RescanException = new InvalidOperationException("Broken");

        Assert.Equal(RescanOutcome.Unreachable, await controller.RescanDevicesAsync(Ct));

        Assert.Equal(1, other.RescanCount);
        Assert.Contains(controller.Devices, device => device.Id == "fan");
    }

    [Fact]
    public async Task Rescan_ListNotLoaded_IsListNotLoadedAndTheListIsTakenOver()
    {
        _dynamic.SetDevices(DynamicDeviceProvider.Device("strip"));
        await _controller.DiscoverAsync(Ct);
        Assert.Contains(_controller.Devices, device => device.Id == "strip");
        _dynamic.RescanOutcome = RescanOutcome.ListNotLoaded;
        _dynamic.DevicesAfterRescan = [];

        Assert.Equal(RescanOutcome.ListNotLoaded, await _controller.RescanDevicesAsync(Ct));

        Assert.DoesNotContain(_controller.Devices, device => device.Id == "strip");
    }

    // Not reached says more than a list that did not come, whichever provider answers first.
    [Theory]
    [InlineData(RescanOutcome.Unreachable, RescanOutcome.ListNotLoaded)]
    [InlineData(RescanOutcome.ListNotLoaded, RescanOutcome.Unreachable)]
    public async Task Rescan_UnreachableAndListNotLoaded_IsUnreachable(RescanOutcome first, RescanOutcome second)
    {
        var other = new DynamicDeviceProvider { RescanOutcome = second };
        using var controller = new LightingController([_dynamic, other]);
        await controller.DiscoverAsync(Ct);
        _dynamic.RescanOutcome = first;

        Assert.Equal(RescanOutcome.Unreachable, await controller.RescanDevicesAsync(Ct));
    }

    [Fact]
    public async Task Rescan_OneListNotLoaded_IsListNotLoaded()
    {
        var other = new DynamicDeviceProvider();
        using var controller = new LightingController([other, _dynamic]);
        await controller.DiscoverAsync(Ct);
        _dynamic.RescanOutcome = RescanOutcome.ListNotLoaded;

        Assert.Equal(RescanOutcome.ListNotLoaded, await controller.RescanDevicesAsync(Ct));
    }

    [Fact]
    public async Task Rescan_NoProviderCanSearch_IsRescanned()
    {
        using var controller = new LightingController([_recording]);
        await controller.DiscoverAsync(Ct);

        Assert.Equal(RescanOutcome.Rescanned, await controller.RescanDevicesAsync(Ct));

        Assert.Single(controller.Devices);
    }
}
