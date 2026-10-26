using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.Simulated;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

public sealed class LightingControllerCustomEffectTests : IDisposable
{
    private const string Disco = "custom:disco";

    private readonly FakeTimeProvider _time = new();
    private readonly RecordingDeviceProvider _provider = new();
    private readonly LightingController _controller;

    public LightingControllerCustomEffectTests() => _controller = new LightingController([_provider], _time);

    public void Dispose() => _controller.Dispose();

    // Red for a second, then blue for a second, without fades.
    private static CustomEffect RedThenBlue { get; } = new(Disco, "Disco", [new CustomEffectStep(Red, 1, 0), new CustomEffectStep(Blue, 1, 0)]);

    [Fact]
    public async Task Apply_OwnEffect_PlaysItsSteps()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.SetCustomEffects([RedThenBlue]);

        _controller.Apply(Uniform(Disco));

        Assert.Equal([Red, Red, Red], _provider.LastColors);
        Assert.True(_controller.IsAnimating);
        _time.Advance(TimeSpan.FromSeconds(1.5));
        await Eventually.TrueAsync(() => _provider.LastColors.SequenceEqual([Blue, Blue, Blue]));
    }

    // A pattern is stretched over the LEDs of the device.
    [Fact]
    public async Task Apply_OwnEffectWithAPattern_StretchesItOverTheDevice()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.SetCustomEffects([new CustomEffect(Disco, "Disco", [new CustomEffectStep([Red, Blue])])]);

        _controller.Apply(Uniform(Disco));

        Assert.Equal([Red, Blue, Blue], _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    [Fact]
    public async Task Apply_OwnEffect_ScaledByTheBrightness()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.SetCustomEffects([RedThenBlue]);

        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 0.5, Disco)));

        Assert.Equal([Red.Scale(0.5), Red.Scale(0.5), Red.Scale(0.5)], _provider.LastColors);
    }

    // At twice the speed, three quarters of a second is a second and a half into the steps.
    [Fact]
    public async Task Apply_OwnEffectAtDoubleSpeed_RunsTwiceAsFast()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.SetCustomEffects([RedThenBlue]);

        _controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, Disco, Speed: 2.0)));
        _time.Advance(TimeSpan.FromSeconds(0.75));

        await Eventually.TrueAsync(() => _provider.LastColors.SequenceEqual([Blue, Blue, Blue]));
    }

    // Such as a scene saved with an effect that was deleted since: the device color, statically.
    [Fact]
    public async Task Apply_OwnEffectThatIsGone_ShowsTheColorStatically()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.SetCustomEffects([]);

        _controller.Apply(Uniform(Disco));

        Assert.Equal(EffectIds.Static, _controller.GetLighting(null).EffectId);
        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    // Before the own effects are known, such as with effects.json out of reach, the lighting keeps
    // the effect; it shows the device color until the effects come.
    [Fact]
    public async Task Apply_OwnEffectBeforeTheEffectsAreKnown_KeepsIt()
    {
        await _controller.DiscoverAsync(Ct);

        _controller.Apply(Uniform(Disco));
        Assert.Equal(Disco, _controller.GetLighting(null).EffectId);
        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
        _controller.SetCustomEffects([RedThenBlue]);

        Assert.Equal(Disco, _controller.GetLighting(null).EffectId);
        Assert.Equal([Red, Red, Red], _provider.LastColors);
    }

    [Fact]
    public async Task Replace_OwnEffectThatIsGoneOnADevice_GivesThatDeviceStatic()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.SetCustomEffects([]);
        var state = LightingStates.WithDevices(
            LightingStates.StaticLighting(Blue),
            new Dictionary<string, DeviceLighting> { [RecordingDeviceProvider.DeviceId] = new(Orange, 1.0, Disco) });

        var applied = _controller.Replace(state);

        Assert.Equal(EffectIds.Static, applied.For(RecordingDeviceProvider.DeviceId).EffectId);
        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    [Fact]
    public async Task SetLighting_OwnEffect_SwitchesToIt()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.SetCustomEffects([RedThenBlue]);
        _controller.Apply(LightingStates.Uniform(LightingStates.StaticLighting(Orange)));

        _controller.SetLighting(RecordingDeviceProvider.DeviceId, lighting => lighting with { EffectId = Disco });

        Assert.Equal(Disco, _controller.GetLighting(RecordingDeviceProvider.DeviceId).EffectId);
        Assert.Equal([Red, Red, Red], _provider.LastColors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(RecordingDeviceProvider.DeviceId)]
    public async Task SetLighting_OwnEffectThatDoesNotExist_ThrowsAndKeepsTheLighting(string? deviceId)
    {
        await _controller.DiscoverAsync(Ct);
        _controller.SetCustomEffects([RedThenBlue]);
        _controller.Apply(LightingStates.Uniform(LightingStates.StaticLighting(Orange)));

        var exception = Assert.Throws<ArgumentException>(() => _controller.SetLighting(deviceId, lighting => lighting with { EffectId = "custom:gone" }));

        Assert.Equal("effectId", exception.ParamName);
        Assert.Equal(EffectIds.Static, _controller.GetLighting(deviceId).EffectId);
    }

    // The devices that show the effect get its new steps at once; that is no change of the lighting.
    [Fact]
    public async Task SetCustomEffects_NewSteps_ShowAtOnce()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.SetCustomEffects([RedThenBlue]);
        _controller.Apply(Uniform(Disco));
        var changes = 0;
        _controller.LightingChanged += (_, _) => changes++;

        _controller.SetCustomEffects([RedThenBlue with { Steps = [new CustomEffectStep(Green)] }]);

        Assert.Equal([Green, Green, Green], _provider.LastColors);
        Assert.Equal(0, changes);
        Assert.False(_controller.IsAnimating);
    }

    // Only an effect with other steps gets a new StepEffect: one that is only renamed, here as a new
    // copy as from the pipe, keeps its own and copies none of its colors.
    [Fact]
    public async Task SetCustomEffects_SameSteps_CopiesNoColors()
    {
        await _controller.DiscoverAsync(Ct);
        RgbColor[] pattern = [.. Enumerable.Range(0, CustomEffects.MaxLeds).Select(i => Twelve[i % Twelve.Count])];
        var big = new CustomEffect("custom:big", "Groot", [.. Enumerable.Range(0, 8).Select(_ => new CustomEffectStep(pattern))]);
        _controller.SetCustomEffects([big, RedThenBlue]);
        _controller.Apply(Uniform(big.Id));
        _controller.SetCustomEffects([big, RedThenBlue with { Steps = [new CustomEffectStep(Blue)] }]);
        var renamed = new CustomEffect(big.Id, "Anders", [.. big.Steps.Select(step => new CustomEffectStep(step.Colors))]);
        var green = RedThenBlue with { Steps = [new CustomEffectStep(Green)] };
        var shown = _provider.LastColors.ToArray();

        var before = GC.GetAllocatedBytesForCurrentThread();
        _controller.SetCustomEffects([renamed, green]);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.InRange(allocated, 0, pattern.Length * 3);
        Assert.Equal(shown, _provider.LastColors);
    }

    // A deleted effect leaves the device color, statically, as a change that is saved.
    [Fact]
    public async Task SetCustomEffects_EffectInUseGone_SwitchesToStatic()
    {
        await _controller.DiscoverAsync(Ct);
        _controller.SetCustomEffects([RedThenBlue]);
        _controller.Apply(LightingStates.WithDevices(
            new DeviceLighting(Blue, 1.0, Disco),
            new Dictionary<string, DeviceLighting> { [RecordingDeviceProvider.DeviceId] = new(Orange, 0.5, Disco) }));
        var changes = 0;
        _controller.LightingChanged += (_, _) => changes++;

        _controller.SetCustomEffects([]);

        Assert.Equal(1, changes);
        Assert.Equal(EffectIds.Static, _controller.GetLighting(null).EffectId);
        Assert.Equal(new DeviceLighting(Orange, 0.5, EffectIds.Static), _controller.GetLighting(RecordingDeviceProvider.DeviceId));
        Assert.Equal([Orange.Scale(0.5), Orange.Scale(0.5), Orange.Scale(0.5)], _provider.LastColors);
        Assert.False(_controller.IsAnimating);
    }

    // Before the lighting is on, the effects are only kept for it.
    [Fact]
    public async Task SetCustomEffects_BeforeTheLighting_ChangesNothing()
    {
        await _controller.DiscoverAsync(Ct);
        var changes = 0;
        _controller.LightingChanged += (_, _) => changes++;

        _controller.SetCustomEffects([RedThenBlue]);

        Assert.Equal(0, changes);
        Assert.Equal(0, _provider.SetLedsCount);
    }

    // With a listener that does nothing, as with the settings app closed; a pattern of a hundred
    // colors on every simulated device, longer or shorter, and a fade between pattern and color.
    [Fact]
    public async Task RenderAnimationFrame_OwnEffect_AllocatesNothing()
    {
        var simulated = new SimulatedDeviceProvider();
        using var controller = new LightingController([simulated], new SteppingTimeProvider(TimeSpan.FromSeconds(0.1)));
        await controller.DiscoverAsync(Ct);
        RgbColor[] pattern = [.. Enumerable.Range(0, 100).Select(i => Twelve[i % Twelve.Count])];
        controller.SetCustomEffects([new CustomEffect(Disco, "Disco", [new CustomEffectStep(pattern, 0.5, 1), new CustomEffectStep(Orange, 0.5, 1)])]);
        controller.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 0.5, Disco)));
        controller.Rendered += (_, _) => { };
        controller.RenderAnimationFrame();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 300; frame++)
        {
            controller.RenderAnimationFrame();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Contains(simulated.GetLeds("simulated-keyboard"), led => led != RgbColor.Black);
    }

    private static LightingState Uniform(string effectId) => LightingStates.Uniform(new DeviceLighting(Orange, 1.0, effectId));
}
