using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class LampsViewModelTests
{
    private readonly LampsViewModel _lamps = new();
    private readonly List<LampSwitch> _switches = [];

    public LampsViewModelTests()
    {
        _lamps.SwitchRequested += (_, lamp) => _switches.Add(lamp);
        _lamps.ShowDevices(
        [
            DeviceItem.AllDevices(3),
            DeviceItem.For(TestDevices.Create("strip", "LED strip", DeviceType.LedStrip, 3)),
            DeviceItem.For(TestDevices.Create("fan", "Fan", DeviceType.Fan, 2)),
            DeviceItem.For(TestDevices.Create("sim", "Simulated", DeviceType.Other, 1, DeviceSource.Simulated)),
        ]);
    }

    [Fact]
    public void ShowDevices_MakesALampOfEveryDeviceButAllDevices()
    {
        Assert.Equal(["strip", "fan", "sim"], _lamps.Lamps.Select(lamp => lamp.DeviceId));
        Assert.True(_lamps.HasLamps);
        Assert.Equal("3 van 3 aan", _lamps.Summary);
    }

    [Fact]
    public async Task RefreshAsync_ShowsWhichLampsAreOnInWordsToo()
    {
        await _lamps.RefreshAsync(All(deviceId => Lighting(on: deviceId != "fan", brightness: 0.8)));

        Assert.Equal(2, _lamps.OnCount);
        Assert.Equal("2 van 3 aan", _lamps.Summary);
        Assert.True(_lamps.IsAnyOn);
        Assert.Equal("LED-strip · Fel rood · 80%", _lamps.Lamps[0].Details);
        Assert.Equal("Ventilator · uit", _lamps.Lamps[1].Details);
        Assert.Equal(["LED strip", "Fan", "Simulated"], _lamps.Lamps.Select(lamp => lamp.ToString()));
        Assert.Empty(_switches);
    }

    [Fact]
    public void Lamp_SwitchedByTheUser_AsksForThatLamp()
    {
        _lamps.Lamps[1].IsOn = false;

        Assert.Equal([new LampSwitch("fan", false)], _switches);
        Assert.Equal("2 van 3 aan", _lamps.Summary);
    }

    [Fact]
    public void IsAnyOn_SwitchedOff_SwitchesTheWholeHouseOffAtOnce()
    {
        _lamps.IsAnyOn = false;

        Assert.Equal([new LampSwitch(null, false)], _switches);
        Assert.All(_lamps.Lamps, lamp => Assert.False(lamp.IsOn));
        Assert.Equal("0 van 3 aan", _lamps.Summary);
        Assert.False(_lamps.IsAnyOn);
    }

    // Real and simulated lamps, then only the simulated one, then none.
    [Fact]
    public void HasOnlySimulatedLamps_OnlyWithSimulatedLampsAndNoOther()
    {
        List<string?> changes = [];
        _lamps.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.False(_lamps.HasOnlySimulatedLamps);

        _lamps.ShowDevices([DeviceItem.AllDevices(2), DeviceItem.For(TestDevices.Create("fan", "Fan", DeviceType.Fan, 1)), DeviceItem.For(TestDevices.Create("sim", "Simulated", DeviceType.Other, 1, DeviceSource.Simulated))]);
        Assert.False(_lamps.HasOnlySimulatedLamps);

        _lamps.ShowDevices([DeviceItem.AllDevices(1), DeviceItem.For(TestDevices.Create("sim", "Simulated", DeviceType.Other, 1, DeviceSource.Simulated))]);
        Assert.True(_lamps.HasOnlySimulatedLamps);
        Assert.Contains(nameof(LampsViewModel.HasOnlySimulatedLamps), changes);

        _lamps.ShowDevices([DeviceItem.AllDevices(0)]);
        Assert.False(_lamps.HasOnlySimulatedLamps);
    }

    [Fact]
    public async Task IsAnyOn_SwitchedOnWithOneLampOn_SwitchesEveryLampOn()
    {
        await _lamps.RefreshAsync(All(deviceId => Lighting(on: deviceId == "fan")));
        Assert.True(_lamps.IsAnyOn);

        _lamps.IsAnyOn = false;
        _lamps.IsAnyOn = true;

        Assert.Equal([new LampSwitch(null, false), new LampSwitch(null, true)], _switches);
        Assert.Equal(3, _lamps.OnCount);
    }

    [Fact]
    public void IsAnyOn_WithoutLamps_AsksNothing()
    {
        _lamps.ShowDevices([DeviceItem.AllDevices(0)]);

        _lamps.IsAnyOn = true;

        Assert.Empty(_switches);
        Assert.False(_lamps.HasLamps);
        Assert.Equal("0 van 0 aan", _lamps.Summary);
    }

    [Fact]
    public async Task SwitchOn_LampThatIsOff_SwitchesOnlyThatLampOn()
    {
        await _lamps.RefreshAsync(All(deviceId => Lighting(on: deviceId == "strip")));

        _lamps.SwitchOn("fan");

        Assert.Equal([new LampSwitch("fan", true)], _switches);
        Assert.Equal([true, true, false], _lamps.Lamps.Select(lamp => lamp.IsOn));
        Assert.Equal("2 van 3 aan", _lamps.Summary);
    }

    [Fact]
    public void SwitchOn_LampThatIsOn_AsksNothing()
    {
        _lamps.SwitchOn("fan");

        Assert.Empty(_switches);
    }

    [Fact]
    public async Task SwitchOn_WholeHouseWithALampOff_SwitchesEveryLampOnAtOnce()
    {
        await _lamps.RefreshAsync(All(deviceId => Lighting(on: deviceId != "fan")));

        _lamps.SwitchOn(null);

        Assert.Equal([new LampSwitch(null, true)], _switches);
        Assert.Equal(3, _lamps.OnCount);
    }

    [Fact]
    public void SwitchOn_WholeHouseAllOn_AsksNothing()
    {
        _lamps.SwitchOn(null);

        Assert.Empty(_switches);
    }

    [Fact]
    public async Task ShowDevices_LampThatStays_KeepsWhetherItIsOn()
    {
        await _lamps.RefreshAsync(All(deviceId => Lighting(on: deviceId != "fan")));

        _lamps.ShowDevices([DeviceItem.For(TestDevices.Create("fan", "Fan", DeviceType.Fan, 2))]);

        Assert.False(Assert.Single(_lamps.Lamps).IsOn);
    }

    [Fact]
    public async Task ShowDevices_SameDevices_KeepsTheSameLampsAndWhatTheyShow()
    {
        await _lamps.RefreshAsync(All(deviceId => Lighting(on: deviceId != "fan", brightness: 0.4)));
        LampItem[] before = [.. _lamps.Lamps];
        List<string?> changes = [];
        _lamps.Lamps.CollectionChanged += (_, e) => changes.Add(e.Action.ToString());

        _lamps.ShowDevices(
        [
            DeviceItem.AllDevices(3),
            DeviceItem.For(TestDevices.Create("strip", "LED strip", DeviceType.LedStrip, 3)),
            DeviceItem.For(TestDevices.Create("fan", "Fan", DeviceType.Fan, 2)),
            DeviceItem.For(TestDevices.Create("sim", "Simulated", DeviceType.Other, 1, DeviceSource.Simulated)),
        ]);

        Assert.Equal(before, _lamps.Lamps);
        Assert.Empty(changes);
        Assert.Equal("LED-strip · Fel rood · 40%", _lamps.Lamps[0].Details);
    }

    [Fact]
    public async Task ShowDevices_ChangedOrNewDevices_ChangeOnlyThoseLamps()
    {
        await _lamps.RefreshAsync(All(deviceId => Lighting(on: deviceId != "fan", brightness: 0.4)));
        var strip = _lamps.Lamps[0];

        _lamps.ShowDevices(
        [
            DeviceItem.For(TestDevices.Create("keyboard", "Keyboard", DeviceType.Keyboard, 4)),
            DeviceItem.For(TestDevices.Create("strip", "LED strip", DeviceType.LedStrip, 3)),
            DeviceItem.For(TestDevices.Create("fan", "Fan", DeviceType.Fan, 2), "Op kanaal 2"),
        ]);

        Assert.Equal(["keyboard", "strip", "fan"], _lamps.Lamps.Select(lamp => lamp.DeviceId));
        Assert.Same(strip, _lamps.Lamps[1]);
        Assert.Equal("Op kanaal 2", _lamps.Lamps[2].Device.LocationLabel);
        Assert.Equal("Ventilator · uit", _lamps.Lamps[2].Details);

        _lamps.Lamps[2].IsOn = true;
        Assert.Equal([new LampSwitch("fan", true)], _switches);
    }

    [Fact]
    public async Task RefreshAsync_AskedWhileRunning_GoesRoundOnceMoreWithTheNewestWay()
    {
        var gate = new TaskCompletionSource();
        List<string> asked = [];
        var first = _lamps.RefreshAsync(async () =>
        {
            asked.Add("old");
            await gate.Task;
            return await All(_ => Lighting(on: true))();
        });

        var second = _lamps.RefreshAsync(() =>
        {
            asked.Add("new");
            return All(_ => Lighting(on: false))();
        });
        Assert.True(second.IsCompleted);
        gate.SetResult();
        await first;

        Assert.Equal(["old", "new"], asked);
        Assert.Equal(0, _lamps.OnCount);
    }

    [Fact]
    public async Task RefreshAsync_FailsWhileANewerOneWaits_GoesOnWithTheNewerOne()
    {
        var gate = new TaskCompletionSource();
        var first = _lamps.RefreshAsync(async () =>
        {
            await gate.Task;
            throw new ObjectDisposedException("connection");
        });

        _ = _lamps.RefreshAsync(All(_ => Lighting(on: false)));
        gate.SetResult();
        await first;

        Assert.Equal(0, _lamps.OnCount);
    }

    [Fact]
    public async Task RefreshAsync_Fails_TellsTheCaller()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _lamps.RefreshAsync(() => throw new InvalidOperationException()));

        // And the next refresh runs again.
        await _lamps.RefreshAsync(All(_ => Lighting(on: false)));
        Assert.Equal(0, _lamps.OnCount);
    }

    [Fact]
    public async Task RefreshAsync_DeviceThatIsNoLamp_IsSkippedAndAMissingLampKeepsItsState()
    {
        await _lamps.RefreshAsync(() => Task.FromResult<IReadOnlyList<DeviceLightingInfo>>(
        [
            new("gone", Lighting(on: false)),
            new("fan", Lighting(on: false)),
        ]));

        Assert.Equal([true, false, true], _lamps.Lamps.Select(lamp => lamp.IsOn));
    }

    [Fact]
    public async Task ShowFrame_GivesEachLampItsBrightestLedAndSpreadsTheStripOverAllLeds()
    {
        await _lamps.RefreshAsync(All(deviceId => Lighting(on: deviceId != "sim")));

        _lamps.ShowFrame(new LightingFrame(1,
        [
            DeviceFrame.FromColors("strip", [Red, new RgbColor(10, 10, 10), Red]),
            DeviceFrame.FromColors("fan", [new RgbColor(0, 0, 50), Blue]),
            DeviceFrame.FromColors("sim", [RgbColor.Black]),
        ]));

        Assert.Equal([Red, Blue, RgbColor.Black], _lamps.Lamps.Select(lamp => lamp.Color));
        Assert.Equal(LampsViewModel.StripLength, _lamps.Strip.Count);
        // Six LEDs over fourteen bulbs: the first bulbs show the strip, the last one the simulated lamp, which is off.
        Assert.Equal(Red, _lamps.Strip[0].Color);
        Assert.True(_lamps.Strip[0].IsLit);
        Assert.Equal(Blue, _lamps.Strip[11].Color);
        Assert.False(_lamps.Strip[^1].IsLit);
    }

    // Kleur asks the engine for the one lamp it shows; such a frame leaves the strip as it was.
    [Fact]
    public async Task ShowFrame_WithSomeOfTheLamps_ShowsThoseAndKeepsTheStrip()
    {
        await _lamps.RefreshAsync(All(_ => Lighting(on: true)));
        _lamps.ShowFrame(new LightingFrame(1,
        [
            DeviceFrame.FromColors("strip", [Red, Red, Red]),
            DeviceFrame.FromColors("fan", [Red, Red]),
            DeviceFrame.FromColors("sim", [Red]),
        ]));

        _lamps.ShowFrame(new LightingFrame(2, [DeviceFrame.FromColors("fan", [Blue, Blue])]));

        Assert.Equal([Red, Blue, Red], _lamps.Lamps.Select(lamp => lamp.Color));
        Assert.All(_lamps.Strip, led => Assert.Equal(Red, led.Color));
    }

    [Fact]
    public void ShowFrame_OlderFrame_IsIgnoredUntilReset()
    {
        _lamps.ShowFrame(new LightingFrame(5, [DeviceFrame.FromColors("fan", [Blue, Blue])]));

        _lamps.ShowFrame(new LightingFrame(4, [DeviceFrame.FromColors("fan", [Red, Red])]));
        Assert.Equal(Blue, _lamps.Lamps[1].Color);

        _lamps.Reset();
        _lamps.ShowFrame(new LightingFrame(1, [DeviceFrame.FromColors("fan", [Red, Red])]));
        Assert.Equal(Red, _lamps.Lamps[1].Color);
    }

    // The card says the name of the one color the lamp shows, or the name of an effect without one
    // color, such as the rainbow or comet with two colors.
    [Theory]
    [InlineData(EffectIds.Static, null, "LED-strip · Warm wit · 100%")]
    [InlineData(EffectIds.Breathing, null, "LED-strip · Warm wit · 100%")]
    [InlineData(EffectIds.Wave, null, "LED-strip · Warm wit · 100%")]
    [InlineData(EffectIds.Comet, null, "LED-strip · Warm wit · 100%")]
    [InlineData(EffectIds.Comet, "#89CFF0", "LED-strip · Babyblauw · 100%")]
    [InlineData(EffectIds.Comet, "#89CFF0,#FF0000", "LED-strip · Komeet · 100%")]
    [InlineData(EffectIds.Fire, null, "LED-strip · Vuur · 100%")]
    [InlineData(EffectIds.Fire, "#FF8800", "LED-strip · Oranje · 100%")]
    [InlineData(EffectIds.Rainbow, null, "LED-strip · Regenboog · 100%")]
    [InlineData(EffectIds.ColorLoop, null, "LED-strip · Kleurenloop · 100%")]
    [InlineData(EffectIds.PerLed, null, "LED-strip · Per LED · 100%")]
    [InlineData("custom:disco", null, "LED-strip · Eigen effect · 100%")]
    public async Task Details_SayTheColorOrTheEffect(string effectId, string? ownColors, string expected)
    {
        var effectColors = ownColors is null ? null
            : EffectColors.None.With(effectId, [.. ownColors.Split(',').Select(hex => RgbColor.TryParseHex(hex, out var color) ? color : throw new ArgumentException(hex))]);

        await _lamps.RefreshAsync(All(_ => new LightingInfo(new RgbColor(255, 244, 229), 1.0, effectId, EffectColors: effectColors)));

        Assert.Equal(expected, _lamps.Lamps[0].Details);
    }

    // An own effect under its own name, also after it got another one; off says only "uit".
    [Fact]
    public async Task Details_NameAnOwnEffectAndSayOffWithoutAColor()
    {
        var name = "Disco";
        _lamps.EffectName = id => id == "custom:disco" ? name : null;
        _lamps.ShowDevices([DeviceItem.For(TestDevices.Create("strip", "LED strip", DeviceType.LedStrip, 3)), DeviceItem.For(TestDevices.Create("other", "Other", DeviceType.Fan, 3))]);
        await _lamps.RefreshAsync(() => Task.FromResult<IReadOnlyList<DeviceLightingInfo>>(
        [
            new("other", new LightingInfo(Red, 0.5, "custom:disco")),
            new("strip", new LightingInfo(Red, 1.0, EffectIds.Static, On: false)),
        ]));
        var other = _lamps.Lamps.Single(lamp => lamp.DeviceId == "other");
        Assert.Equal("Ventilator · Disco · 50%", other.Details);
        Assert.Equal("LED-strip · uit", _lamps.Lamps[0].Details);

        name = "Feest";
        _lamps.ShowEffectNames();

        Assert.Equal("Ventilator · Feest · 50%", other.Details);
    }

    // Names that come after the lamps reach their cards too.
    [Fact]
    public async Task EffectName_SetLater_NamesTheEffectOnTheCardsThereAre()
    {
        await _lamps.RefreshAsync(All(_ => new LightingInfo(Red, 1.0, "custom:disco")));
        Assert.Equal("LED-strip · Eigen effect · 100%", _lamps.Lamps[0].Details);

        _lamps.EffectName = id => id == "custom:disco" ? "Disco" : null;

        Assert.Equal("LED-strip · Disco · 100%", _lamps.Lamps[0].Details);
    }

    // A new color, such as each step of a drag on Kleur, changes the card; the same lighting again
    // or a frame changes nothing, so the card does not flicker with an effect that moves.
    [Fact]
    public async Task Details_ChangeWithTheColorNotWithTheFrames()
    {
        var lamp = _lamps.Lamps[0];
        await _lamps.RefreshAsync(All(_ => Lighting(on: true)));
        List<string> details = [];
        lamp.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LampItem.Details))
            {
                details.Add(lamp.Details);
            }
        };

        await _lamps.RefreshAsync(All(_ => Lighting(on: true)));
        _lamps.ShowFrame(new LightingFrame(1, [DeviceFrame.FromColors("strip", [Blue, Blue, Blue])]));
        await _lamps.RefreshAsync(All(_ => Lighting(on: true) with { Color = new RgbColor(128, 0, 0) }));

        Assert.Equal(["LED-strip · Donkerrood · 100%"], details);
    }

    [Fact]
    public void Lamp_NamesItsSwitchAndItsCardForAScreenReader()
    {
        Assert.Equal("Fan aan of uit", _lamps.Lamps[1].SwitchAccessibleName);
        Assert.Equal("Fan: kleur en effect", _lamps.Lamps[1].OpenAccessibleName);
        Assert.Equal(Strings.DeviceSource_Simulated, _lamps.Lamps[2].Device.Group);
    }

    // The lighting of the three lamps of the test, as the engine gives it in one call.
    private static Func<Task<IReadOnlyList<DeviceLightingInfo>>> All(Func<string, LightingInfo> lighting) =>
        () => Task.FromResult<IReadOnlyList<DeviceLightingInfo>>([.. new[] { "strip", "fan", "sim" }.Select(id => new DeviceLightingInfo(id, lighting(id)))]);

    private static LightingInfo Lighting(bool on, double brightness = 1.0) => new(Red, brightness, EffectIds.Static, On: on);
}
