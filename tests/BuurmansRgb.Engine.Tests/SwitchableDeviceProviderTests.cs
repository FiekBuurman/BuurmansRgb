using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine.Tests;

public sealed class SwitchableDeviceProviderTests
{
    private readonly SimulatedDeviceProvider _simulated = new();
    private readonly SwitchableDeviceProvider _switchable;
    private int _changes;

    public SwitchableDeviceProviderTests()
    {
        _switchable = new SwitchableDeviceProvider(_simulated);
        _switchable.DevicesChanged += (_, _) => _changes++;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Enabled_PassesDevicesAndColorsThrough()
    {
        Assert.Equal(await _simulated.DiscoverAsync(Ct), await _switchable.DiscoverAsync(Ct));

        _switchable.SetLeds("simulated-fan", Enumerable.Repeat(RgbColor.White, 12).ToArray());

        Assert.All(_simulated.GetLeds("simulated-fan").ToArray(), led => Assert.Equal(RgbColor.White, led));
        Assert.Equal("Simulated", _switchable.Name);
    }

    [Fact]
    public async Task Disabled_HasNoDevicesAndSendsNothing()
    {
        _switchable.IsEnabled = false;

        Assert.Empty(await _switchable.DiscoverAsync(Ct));
        _switchable.SetLeds("simulated-fan", Enumerable.Repeat(RgbColor.White, 12).ToArray());
        Assert.All(_simulated.GetLeds("simulated-fan").ToArray(), led => Assert.Equal(RgbColor.Black, led));
    }

    [Fact]
    public async Task IsEnabled_BeforeFirstDiscover_RaisesNothing()
    {
        _switchable.IsEnabled = false;

        Assert.Empty(await _switchable.DiscoverAsync(Ct));
        Assert.Equal(0, _changes);
    }

    [Fact]
    public async Task IsEnabled_AfterDiscover_RaisesDevicesChangedOnlyWhenItChanges()
    {
        await _switchable.DiscoverAsync(Ct);
        _switchable.IsEnabled = true;
        Assert.Equal(0, _changes);

        _switchable.IsEnabled = false;
        _switchable.IsEnabled = false;
        Assert.Equal(1, _changes);

        _switchable.IsEnabled = true;
        Assert.Equal(2, _changes);
    }

    [Fact]
    public async Task ResizeZone_Enabled_ResizesTheInnerZoneAndPassesTheChangeOn()
    {
        _switchable.ResizeZone("simulated-strip", 0, 40);

        var strip = (await _switchable.DiscoverAsync(Ct)).Single(device => device.Id == "simulated-strip");
        Assert.Equal(40, strip.LedCount);
        Assert.Equal(1, _changes);
    }

    [Fact]
    public void ResizeZone_Disabled_ThrowsAndLeavesTheZone()
    {
        _switchable.IsEnabled = false;

        Assert.Throws<ArgumentException>(() => _switchable.ResizeZone("simulated-strip", 0, 40));
        Assert.Equal(30, _simulated.GetLeds("simulated-strip").Length);
    }

    [Fact]
    public async Task InnerDevicesChanged_WhileDisabled_RaisesNothing()
    {
        await _switchable.DiscoverAsync(Ct);
        _switchable.IsEnabled = false;
        _changes = 0;

        _simulated.ResizeZone("simulated-strip", 0, 40);

        Assert.Equal(0, _changes);
    }

    [Fact]
    public void ResizeZone_InnerCannotResize_Throws()
    {
        var switchable = new SwitchableDeviceProvider(new RecordingDeviceProvider());

        Assert.Throws<NotSupportedException>(() => switchable.ResizeZone("device", 0, 4));
    }
}
