using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Effects;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

// The direction of the effects that run over the LEDs, with every simulated device. The frames are
// rendered by hand after moving the fake clock.
public sealed class LightingControllerDirectionTests : IDisposable
{
    private const string Keyboard = "simulated-keyboard";
    private const string Strip = "simulated-strip";
    private const string Fan = "simulated-fan";

    private static readonly TimeSpan Later = TimeSpan.FromSeconds(1.7);

    private readonly FakeTimeProvider _time = new();
    private readonly SimulatedDeviceProvider _provider = new();
    private readonly LightingController _controller;

    public LightingControllerDirectionTests() => _controller = new LightingController([_provider], _time);

    public void Dispose() => _controller.Dispose();

    public static TheoryData<string> DirectedEffects => [EffectIds.Rainbow, EffectIds.Wave, EffectIds.ColorLoop];

    // Without a direction every device shows what it showed before there was one: the effect over
    // the LEDs in their order, to the bit.
    [Theory]
    [MemberData(nameof(DirectedEffects))]
    public async Task Render_WithoutDirection_IsTheImageByIndexOnEveryDevice(string effectId)
    {
        await StartAsync(effectId);

        AdvanceAndRender(Later);

        foreach (var device in _controller.Devices)
        {
            Assert.Equal(ByIndex(effectId, device.LedCount, Later), _provider.GetLeds(device.Id));
        }
    }

    // The rainbow as it was written before there was a direction.
    [Fact]
    public async Task Render_WithoutDirection_IsTheRainbowOfBefore()
    {
        await StartAsync(EffectIds.Rainbow);

        AdvanceAndRender(Later);

        var keyboard = _provider.GetLeds(Keyboard);
        var shift = Later.TotalSeconds * RainbowEffect.DefaultRoundsPerSecond;
        for (var i = 0; i < keyboard.Length; i++)
        {
            Assert.Equal(RgbColor.FromHsv(i / (double)keyboard.Length + shift, 1.0, 1.0), keyboard[i]);
        }
    }

    [Theory]
    [MemberData(nameof(DirectedEffects))]
    public async Task Render_DirectionClearedAgain_IsTheImageByIndex(string effectId)
    {
        await StartAsync(effectId);
        _controller.SetLighting(null, lighting => lighting with { Direction = 45 });

        _controller.SetLighting(null, lighting => lighting with { Direction = null });
        AdvanceAndRender(Later);

        Assert.Equal(ByIndex(effectId, _provider.GetLeds(Keyboard).Length, Later), _provider.GetLeds(Keyboard));
    }

    [Theory]
    [MemberData(nameof(DirectedEffects))]
    public async Task Render_WithDirection_RunsOverThePlacesOfTheLeds(string effectId)
    {
        await StartAsync(effectId);

        _controller.SetLighting(null, lighting => lighting with { Direction = 45 });
        AdvanceAndRender(Later);

        foreach (var device in _controller.Devices)
        {
            Assert.Equal(ByPlace(effectId, LedPlaces.Along(device, 45), Later), _provider.GetLeds(device.Id));
        }

        Assert.NotEqual(ByIndex(effectId, _provider.GetLeds(Keyboard).Length, Later), _provider.GetLeds(Keyboard));
    }

    // A strip only goes forward or backward: at 180 degrees the image turns round.
    [Fact]
    public async Task Render_StripAt180_TurnsTheImageRound()
    {
        await StartAsync(EffectIds.Rainbow);
        AdvanceAndRender(Later);
        var forward = _provider.GetLeds(Strip);

        _controller.SetLighting(Strip, lighting => lighting with { Direction = 180 });

        var backward = _provider.GetLeds(Strip);
        for (var i = 0; i < forward.Length; i++)
        {
            AssertClose(forward[forward.Length - 1 - i], backward[i]);
        }
    }

    // Static, the comet and the others do not run over the LEDs in a way a direction changes.
    [Theory]
    [InlineData(EffectIds.Static)]
    [InlineData(EffectIds.Breathing)]
    [InlineData(EffectIds.Comet)]
    [InlineData(EffectIds.Scanner)]
    [InlineData(EffectIds.Fire)]
    public async Task Render_EffectWithoutDirection_IgnoresIt(string effectId)
    {
        await StartAsync(effectId);
        AdvanceAndRender(Later);
        var before = _provider.GetLeds(Keyboard);

        _controller.SetLighting(null, lighting => lighting with { Direction = 90 });

        Assert.Equal(before, _provider.GetLeds(Keyboard));
    }

    [Fact]
    public async Task SetLighting_DirectionOfOneDevice_LeavesTheOthers()
    {
        await StartAsync(EffectIds.Rainbow);

        _controller.SetLighting(Fan, lighting => lighting with { Direction = 90 });

        Assert.Equal(90, _controller.GetLighting(Fan).Direction);
        Assert.Null(_controller.GetLighting(Keyboard).Direction);
        Assert.Equal(ByPlace(EffectIds.Rainbow, LedPlaces.Along(Device(Fan), 90), TimeSpan.Zero), _provider.GetLeds(Fan));
    }

    // Zones alike renders the longest zone over its own places and stretches it over the others.
    [Fact]
    public async Task Render_ZonesAlikeWithDirection_RendersTheLongestZoneOverItsOwnPlaces()
    {
        await StartAsync(EffectIds.Rainbow);
        var dual = Device("simulated-fan-dual");
        _controller.ZonesAlike = new HashSet<string>([dual.Id]);

        _controller.SetLighting(dual.Id, lighting => lighting with { Direction = 90 });

        var parts = RgbZone.PartsOf(dual.Zones, dual.LedCount);
        var longest = Enumerable.Range(0, parts.Length).MaxBy(part => parts[part].LedCount);
        var (first, count) = parts[longest];
        var expected = ByPlace(EffectIds.Rainbow, LedPlaces.Along(dual.Type, dual.Zones[longest], count, 90), TimeSpan.Zero);
        Assert.Equal(expected, _provider.GetLeds(dual.Id).AsSpan(first, count).ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(360)]
    public async Task SetLighting_DirectionOutOfRange_ThrowsAndKeepsTheLighting(int degrees)
    {
        await StartAsync(EffectIds.Rainbow);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => _controller.SetLighting(null, lighting => lighting with { Direction = degrees }));

        Assert.Equal("degrees", exception.ParamName);
        Assert.Null(_controller.GetLighting(null).Direction);
    }

    // As RenderAnimationFrame_WithoutASettingsApp_AllocatesNothing, with a direction on every device
    // and zones alike on those with more zones: the places are made when the direction is set.
    [Theory]
    [MemberData(nameof(DirectedEffects))]
    public async Task RenderAnimationFrame_WithDirection_AllocatesNothing(string effectId)
    {
        var frame = TimeSpan.FromSeconds(1.0 / LightingController.DefaultFramesPerSecond);
        var simulated = new SimulatedDeviceProvider();
        using var controller = new LightingController([simulated], new SteppingTimeProvider(frame));
        await controller.DiscoverAsync(Ct);
        controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 0.5, effectId) { Direction = 45 }));
        controller.ZonesAlike = new HashSet<string>(controller.Devices.Where(device => device.Id != Keyboard && device.Zones.Count > 1).Select(device => device.Id));
        controller.Rendered += (_, _) => { };
        controller.RenderAnimationFrame();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var step = 0; step < 300; step++)
        {
            controller.RenderAnimationFrame();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Contains(simulated.GetLeds(Keyboard), led => led != RgbColor.Black);
    }

    private static IDirectedEffect Create(string effectId) => (IDirectedEffect)EffectCatalog.Create(effectId, Orange, []);

    private static RgbColor[] ByIndex(string effectId, int ledCount, TimeSpan time)
    {
        var leds = new RgbColor[ledCount];
        Create(effectId).Render(leds, time);
        return leds;
    }

    private static RgbColor[] ByPlace(string effectId, double[] positions, TimeSpan time)
    {
        var leds = new RgbColor[positions.Length];
        Create(effectId).Render(leds, positions, time);
        return leds;
    }

    // The places of a strip at 180 degrees are the index the other way round, up to rounding.
    private static void AssertClose(RgbColor expected, RgbColor actual)
    {
        Assert.InRange(actual.R, expected.R - 1, expected.R + 1);
        Assert.InRange(actual.G, expected.G - 1, expected.G + 1);
        Assert.InRange(actual.B, expected.B - 1, expected.B + 1);
    }

    private RgbDevice Device(string deviceId) => _controller.Devices.Single(device => device.Id == deviceId);

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
