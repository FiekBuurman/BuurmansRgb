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
        _switchable = SwitchableDeviceProvider.For(_simulated);
        _switchable.DevicesChanged += (_, _) => _changes++;
    }

    private IResizableDeviceProvider Resizable => Assert.IsAssignableFrom<IResizableDeviceProvider>(_switchable);

    [Fact]
    public async Task Enabled_PassesDevicesAndColorsThrough()
    {
        Assert.Equal(await _simulated.DiscoverAsync(Ct), await _switchable.DiscoverAsync(Ct));

        _switchable.SetLeds("simulated-fan", Enumerable.Repeat(RgbColor.White, 12).ToArray());

        Assert.All(_simulated.GetLeds("simulated-fan"), led => Assert.Equal(RgbColor.White, led));
        Assert.Equal("Simulated", _switchable.Name);
    }

    [Fact]
    public void For_RestorableProvider_IsRefused()
    {
        Assert.Throws<NotSupportedException>(() => SwitchableDeviceProvider.For(new RestorableDeviceProvider()));
    }

    [Fact]
    public async Task Disabled_HasNoDevicesAndSendsNothing()
    {
        _switchable.IsEnabled = false;

        Assert.Empty(await _switchable.DiscoverAsync(Ct));
        _switchable.SetLeds("simulated-fan", Enumerable.Repeat(RgbColor.White, 12).ToArray());
        Assert.All(_simulated.GetLeds("simulated-fan"), led => Assert.Equal(RgbColor.Black, led));
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
        Resizable.ResizeZone("simulated-strip", 0, 40);

        var strip = (await _switchable.DiscoverAsync(Ct)).Single(device => device.Id == "simulated-strip");
        Assert.Equal(40, strip.LedCount);
        Assert.Equal(1, _changes);
    }

    [Fact]
    public void ResizeZone_Disabled_ThrowsAndLeavesTheZone()
    {
        _switchable.IsEnabled = false;

        Assert.Throws<ArgumentException>(() => Resizable.ResizeZone("simulated-strip", 0, 40));
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
    public void For_InnerCannotResize_IsNotResizableEither()
    {
        var switchable = SwitchableDeviceProvider.For(new RecordingDeviceProvider());

        Assert.False(switchable is IResizableDeviceProvider);
        Assert.False(switchable is IRescannableDeviceProvider);
        Assert.Equal("Recording", switchable.Name);
    }

    [Fact]
    public async Task RequestRescan_Enabled_SearchesAgainAndPassesTheChangeOn()
    {
        await _switchable.DiscoverAsync(Ct);
        var rescannable = Assert.IsAssignableFrom<IRescannableDeviceProvider>(_switchable);

        Assert.True(await rescannable.RequestRescanAsync(Ct));

        Assert.Equal(1, _changes);
    }

    [Fact]
    public async Task RequestRescan_Disabled_DoesNothingAndCountsAsDone()
    {
        var inner = new DynamicDeviceProvider { RescanReaches = false };
        var switchable = SwitchableDeviceProvider.For(inner);
        switchable.IsEnabled = false;

        Assert.True(await Assert.IsAssignableFrom<IRescannableDeviceProvider>(switchable).RequestRescanAsync(Ct));

        Assert.Equal(0, inner.RescanCount);
    }

    // A provider that can search again but not resize keeps only the first.
    [Fact]
    public async Task For_InnerCanRescanButNotResize_IsOnlyRescannable()
    {
        var inner = new DynamicDeviceProvider { RescanReaches = false };
        var switchable = SwitchableDeviceProvider.For(inner);

        Assert.False(switchable is IResizableDeviceProvider);
        Assert.False(await Assert.IsAssignableFrom<IRescannableDeviceProvider>(switchable).RequestRescanAsync(Ct));
        Assert.Equal(1, inner.RescanCount);
    }
}
