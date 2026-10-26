using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.OpenRgb.Testing;
using Microsoft.Extensions.Time.Testing;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

// "Verversen" against the OpenRGB provider, with a server in memory that only sees a device that
// came back once it searches.
public sealed class LightingControllerOpenRgbRescanTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly InMemoryOpenRgb _openRgb;
    private readonly LightingController _lighting;

    public LightingControllerOpenRgbRescanTests()
    {
        _openRgb = new InMemoryOpenRgb(_time, TimeSpan.FromSeconds(5));
        _lighting = new LightingController([_openRgb.Provider]);
    }

    public void Dispose()
    {
        _lighting.Dispose();
        _openRgb.Dispose();
    }

    // The headset that is switched off and on again: after the rescan it is in the list again and
    // shows its own lighting.
    [Fact]
    public async Task Rescan_HeadsetSwitchedOffAndOn_IsListedAgainAndTakesItsColors()
    {
        var ids = _openRgb.SetStrips(("Strip", 3), ("Headset", 2));
        var headset = ids[1];
        await _lighting.DiscoverAsync(Ct);
        await Eventually.TrueAsync(() => _lighting.Devices.Count == 2);
        _lighting.Apply(WithDevices(StaticLighting(Orange), new Dictionary<string, DeviceLighting> { [headset] = StaticLighting(Green) }));

        // Switched off: OpenRGB notices and leaves it out.
        _openRgb.SetStrips(("Strip", 3));
        _openRgb.Rescan();
        await Eventually.TrueAsync(() => _lighting.Devices.Count == 1);

        // Switched on again: OpenRGB only sees it once it searches.
        _openRgb.SetStrips(("Strip", 3), ("Headset", 2));
        await _openRgb.WhenIdleAsync();
        Assert.DoesNotContain(_lighting.Devices, device => device.Id == headset);

        // The headset was green already before it went; only what comes after the rescan counts.
        var before = _openRgb.Updates.Count;
        Assert.True(await _lighting.RescanDevicesAsync(Ct));

        Assert.Contains(_lighting.Devices, device => device.Id == headset);
        Assert.Contains(_openRgb.Updates.Skip(before), update => update.Index == 1 && update.Colors.SequenceEqual([Green, Green]));
    }

    [Fact]
    public async Task Rescan_OpenRgbDoesNotRun_IsFalse()
    {
        _openRgb.Stop();
        await _lighting.DiscoverAsync(Ct);
        await _openRgb.WhenIdleAsync();

        Assert.False(await _lighting.RescanDevicesAsync(Ct));

        Assert.Empty(_lighting.Devices);
    }
}
