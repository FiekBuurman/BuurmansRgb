using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

// Zones alike: every zone of a device shows the image the effect renders over its longest zone.
// The frames are rendered by hand after moving the fake clock.
public sealed class LightingControllerZonesAlikeTests : IDisposable
{
    private const string Mouse = "mouse";
    private const string Fan = "fan";
    private const string Board = "board";

    private readonly FakeTimeProvider _time = new();
    private readonly DynamicDeviceProvider _provider = new();
    private readonly LightingController _controller;

    public LightingControllerZonesAlikeTests()
    {
        _provider.SetDevices(MouseWithTwoZones(), DynamicDeviceProvider.Device(Fan, ledCount: 4));
        _controller = new LightingController([_provider], _time);
    }

    public void Dispose() => _controller.Dispose();

    [Fact]
    public async Task Rainbow_WithoutZonesAlike_GivesTheZonesOfOneLedEachTheirOwnColor()
    {
        await StartAsync(EffectIds.Rainbow);

        var leds = _provider.Leds[Mouse];
        Assert.NotEqual(leds[0], leds[1]);
    }

    // The mouse of the user: a primary zone and a logo zone of one LED each.
    [Fact]
    public async Task SetZonesAlike_Rainbow_GivesBothZonesTheSameColorAndKeepsItMoving()
    {
        await StartAsync(EffectIds.Rainbow);

        _controller.SetZonesAlike(Mouse, true);
        var first = _provider.Leds[Mouse];
        AdvanceAndRender(TimeSpan.FromSeconds(1));

        var leds = _provider.Leds[Mouse];
        Assert.Equal(first[0], first[1]);
        Assert.Equal(leds[0], leds[1]);
        Assert.NotEqual(first[0], leds[0]);
    }

    [Fact]
    public async Task SetZonesAlike_LeavesTheOtherDevicesAsTheyAre()
    {
        await StartAsync(EffectIds.Rainbow);
        var fan = _provider.Leds[Fan];

        _controller.SetZonesAlike(Mouse, true);

        Assert.Equal(fan, _provider.Leds[Fan]);
        Assert.Equal(4, fan.Distinct().Count());
    }

    [Fact]
    public async Task SetZonesAlike_Off_RendersOverTheWholeDeviceAgain()
    {
        await StartAsync(EffectIds.Rainbow);
        var before = _provider.Leds[Mouse];
        _controller.SetZonesAlike(Mouse, true);

        _controller.SetZonesAlike(Mouse, false);

        Assert.Equal(before, _provider.Leds[Mouse]);
    }

    // Every LED of the per-LED effect has a color of its own, so nothing is stretched over it.
    [Fact]
    public async Task SetZonesAlike_PerLed_KeepsTheColorOfEveryLed()
    {
        await StartAsync(EffectIds.Static);
        _controller.SetLedColors(Mouse, 0, [Red, Blue]);

        _controller.SetZonesAlike(Mouse, true);

        Assert.Equal([Red, Blue], _provider.Leds[Mouse]);
    }

    [Fact]
    public async Task SetZonesAlike_PerLedAfterAnotherEffect_ShowsTheColorsOfTheLeds()
    {
        await StartAsync(EffectIds.Rainbow);
        _controller.SetZonesAlike(Mouse, true);

        _controller.SetLedColors(Mouse, 0, [Red, Blue]);

        Assert.Equal([Red, Blue], _provider.Leds[Mouse]);
    }

    [Fact]
    public async Task SetZonesAlike_UnknownDevice_ThrowsAndChangesNothing()
    {
        await StartAsync(EffectIds.Rainbow);
        var changes = 0;
        _controller.ZonesAlikeChanged += (_, _) => changes++;

        Assert.Throws<ArgumentException>(() => _controller.SetZonesAlike("unknown", true));

        Assert.Empty(_controller.ZonesAlike);
        Assert.Equal(0, changes);
    }

    // It is no part of the lighting, so it is not saved as one and a scene that is on stays on.
    [Fact]
    public async Task SetZonesAlike_RaisesZonesAlikeChangedOncePerChange_AndNoLightingChanged()
    {
        await StartAsync(EffectIds.Rainbow);
        var changes = 0;
        var lightingChanges = 0;
        _controller.ZonesAlikeChanged += (_, _) => changes++;
        _controller.LightingChanged += (_, _) => lightingChanges++;

        _controller.SetZonesAlike(Mouse, true);
        _controller.SetZonesAlike(Mouse, true);
        _controller.SetZonesAlike(Mouse, false);
        _controller.SetZonesAlike(Mouse, false);

        Assert.Equal(2, changes);
        Assert.Equal(0, lightingChanges);
    }

    [Fact]
    public async Task SetZonesAlike_RaisesRenderedSoThePreviewFollows()
    {
        await StartAsync(EffectIds.Rainbow);
        var rendered = 0;
        _controller.Rendered += (_, _) => rendered++;

        _controller.SetZonesAlike(Mouse, true);

        Assert.Equal(1, rendered);
    }

    // As from settings.json, before the devices are there: a device in the set has it once it
    // comes, and keeps it while it is gone.
    [Fact]
    public async Task ZonesAlike_FromTheSettings_AppliesToADeviceThatComesLater()
    {
        _controller.ZonesAlike = new HashSet<string> { "later" };
        await StartAsync(EffectIds.Rainbow);
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _controller.Rendered += (_, _) => rendered.TrySetResult();

        _provider.SetDevices(MouseWithTwoZones("later"));
        await rendered.Task.WaitAsync(TestTimeouts.Short, Ct);

        var leds = _provider.Leds["later"];
        Assert.Equal(leds[0], leds[1]);
        Assert.Equal(["later"], _controller.ZonesAlike);
    }

    [Fact]
    public async Task ZonesAlike_SameSet_RaisesNothing()
    {
        await StartAsync(EffectIds.Rainbow);
        _controller.ZonesAlike = new HashSet<string> { Mouse };
        var changes = 0;
        _controller.ZonesAlikeChanged += (_, _) => changes++;

        _controller.ZonesAlike = new HashSet<string> { Mouse };

        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task ZonesAlike_AnotherSet_ShowsItAtOnce()
    {
        await StartAsync(EffectIds.Rainbow);

        _controller.ZonesAlike = new HashSet<string> { Mouse };

        var leds = _provider.Leds[Mouse];
        Assert.Equal(leds[0], leds[1]);
    }

    // The user sets another length for a zone: OpenRGB reports the device again with the same id,
    // and the new length gets the image as well. Each LED takes the LED of the image at its middle.
    [Fact]
    public async Task DevicesChanged_ZoneGotLonger_StretchesTheImageOverItsNewLength()
    {
        await StartAsync(EffectIds.Rainbow);
        await WhileDevicesChangeAsync(() => _provider.SetDevices(BoardWith(headerLeds: 2)));
        _controller.SetZonesAlike(Board, true);

        await WhileDevicesChangeAsync(() => _provider.SetDevices(BoardWith(headerLeds: 3)));

        var leds = _provider.Leds[Board];
        Assert.Equal(7, leds.Length);
        Assert.Equal(4, leds[..4].Distinct().Count());
        Assert.Equal([leds[0], leds[2], leds[3]], leds[4..]);
    }

    // As "ARGB 2" that got its first LEDs: now the longest zone, so the image is rendered over it
    // and the zone before it gets the stretched image.
    [Fact]
    public async Task DevicesChanged_ZoneGotItsFirstLeds_RendersTheImageOverItsFullLength()
    {
        await StartAsync(EffectIds.Rainbow);
        await WhileDevicesChangeAsync(() => _provider.SetDevices(BoardWith(headerLeds: 0)));
        _controller.SetZonesAlike(Board, true);

        await WhileDevicesChangeAsync(() => _provider.SetDevices(BoardWith(headerLeds: 5)));

        var leds = _provider.Leds[Board];
        Assert.Equal(9, leds.Length);
        Assert.Equal(5, leds[4..].Distinct().Count());
        Assert.Equal([leds[4], leds[5], leds[7], leds[8]], leds[..4]);
    }

    // A scene that loads replaces the lighting, not how the zones of a device show it.
    [Fact]
    public async Task Replace_KeepsZonesAlike()
    {
        await StartAsync(EffectIds.Static);
        _controller.SetZonesAlike(Mouse, true);

        _controller.Replace(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Rainbow)));

        var leds = _provider.Leds[Mouse];
        Assert.Equal(leds[0], leds[1]);
        Assert.Equal([Mouse], _controller.ZonesAlike);
    }

    // The flash of one zone still covers only that zone.
    [Fact]
    public async Task FlashZone_WithZonesAlike_FlashesOnlyThatZone()
    {
        await StartAsync(EffectIds.Rainbow);
        _controller.SetZonesAlike(Mouse, true);

        _controller.FlashZone(Mouse, 1);

        var leds = _provider.Leds[Mouse];
        Assert.Equal(RgbColor.White, leds[1]);
        Assert.NotEqual(RgbColor.White, leds[0]);
    }

    // As RenderAnimationFrame_WithoutASettingsApp_AllocatesNothing, with every simulated device
    // that has more zones showing them alike.
    [Fact]
    public async Task RenderAnimationFrame_WithZonesAlike_AllocatesNothing()
    {
        var frame = TimeSpan.FromSeconds(1.0 / LightingController.DefaultFramesPerSecond);
        var simulated = new SimulatedDeviceProvider();
        using var controller = new LightingController([simulated], new SteppingTimeProvider(frame));
        await controller.DiscoverAsync(Ct);
        controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 0.5, EffectIds.Rainbow)));
        controller.ZonesAlike = new HashSet<string>(controller.Devices.Where(device => device.Zones.Count > 1).Select(device => device.Id));
        controller.Rendered += (_, _) => { };
        controller.RenderAnimationFrame();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var step = 0; step < 300; step++)
        {
            controller.RenderAnimationFrame();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        var headset = simulated.GetLeds("simulated-headset");
        Assert.Equal(headset[0], headset[1]);
    }

    private static RgbDevice MouseWithTwoZones(string id = Mouse) =>
        new(id, "Mouse", DeviceType.Mouse, [RgbZone.Fixed("Primary Zone", 1), RgbZone.Fixed("Logo Zone", 1)]) { Source = DeviceSource.Hardware };

    // A board with a fixed zone of four LEDs and an addressable header of the given length.
    private static RgbDevice BoardWith(int headerLeds) =>
        new(Board, "Board", DeviceType.Motherboard, [RgbZone.Fixed("Mainboard", 4), new RgbZone("ARGB 2", headerLeds, 0, 60)])
        {
            Source = DeviceSource.Hardware,
        };

    private async Task WhileDevicesChangeAsync(Action change)
    {
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnRendered(object? sender, EventArgs e) => rendered.TrySetResult();
        _controller.Rendered += OnRendered;
        try
        {
            change();
            await rendered.Task.WaitAsync(TestTimeouts.Short, Ct);
        }
        finally
        {
            _controller.Rendered -= OnRendered;
        }
    }

    private async Task StartAsync(string effectId)
    {
        await _controller.DiscoverAsync(Ct);
        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, effectId)));
    }

    private void AdvanceAndRender(TimeSpan time)
    {
        _time.Advance(time);
        _controller.RenderAnimationFrame();
    }
}
