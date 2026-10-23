using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Effects;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class DeviceOutputTests
{
    private readonly RecordingDeviceProvider _provider = new();
    private readonly DeviceOutput _output;

    public DeviceOutputTests() => _output = new DeviceOutput(_provider, _provider.Device);

    [Fact]
    public void Render_ScalesTheEffectByTheBrightness()
    {
        _output.Show(new StaticEffect(Orange), 0.5, EffectSpeed.Default);

        _output.Render(TimeSpan.Zero);

        Assert.All(_output.Buffer, led => Assert.Equal(Orange.Scale(0.5), led));
    }

    [Fact]
    public void Render_FullBrightness_KeepsTheColor()
    {
        _output.Show(new StaticEffect(Orange), 1.0, EffectSpeed.Default);

        _output.Render(TimeSpan.Zero);

        Assert.All(_output.Buffer, led => Assert.Equal(Orange, led));
    }

    [Fact]
    public void Send_SendsTheBufferToTheProvider()
    {
        _output.Show(new StaticEffect(Orange), 1.0, EffectSpeed.Default);
        _output.Render(TimeSpan.Zero);

        _output.Send();

        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.0)]
    public void Send_Scaled_DimsWhatGoesOutButKeepsTheBuffer(double scale)
    {
        _output.Show(new StaticEffect(Orange), 1.0, EffectSpeed.Default);
        _output.Render(TimeSpan.Zero);

        _output.Send(scale);

        Assert.Equal([Orange.Scale(scale), Orange.Scale(scale), Orange.Scale(scale)], _provider.LastColors);
        Assert.All(_output.Buffer, led => Assert.Equal(Orange, led));
    }

    // What the device got was dimmed, so the real colors differ from it and go out.
    [Fact]
    public void SendIfChanged_AfterAScaledSend_SendsTheRealColors()
    {
        _output.Show(new StaticEffect(Orange), 1.0, EffectSpeed.Default);
        _output.Render(TimeSpan.Zero);
        _output.Send(0.0);

        Assert.True(_output.SendIfChanged());
        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    [Fact]
    public void SendIfChanged_SameColorsAsLastSent_SendsNothing()
    {
        _output.Show(new StaticEffect(Orange), 1.0, EffectSpeed.Default);
        _output.Render(TimeSpan.Zero);
        _output.Send();

        _output.Render(TimeSpan.Zero);

        Assert.False(_output.SendIfChanged());
        Assert.Equal(1, _provider.SetLedsCount);
    }

    [Fact]
    public void SendIfChanged_OtherColors_SendsThem()
    {
        _output.Show(new StaticEffect(Orange), 1.0, EffectSpeed.Default);
        _output.Render(TimeSpan.Zero);
        _output.Send();
        _output.Show(new StaticEffect(Blue), 1.0, EffectSpeed.Default);
        _output.Render(TimeSpan.Zero);

        Assert.True(_output.SendIfChanged());
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
    }

    [Fact]
    public void IsAnimated_FollowsTheEffect()
    {
        Assert.False(_output.IsAnimated);

        _output.Show(new RainbowEffect(), 1.0, EffectSpeed.Default);

        Assert.True(_output.IsAnimated);
    }

    [Fact]
    public void Render_WithoutZonesAlike_RendersOverTheWholeDevice()
    {
        var output = OutputFor(RgbZone.Fixed("Logo", 1), RgbZone.Fixed("Side", 4), RgbZone.Fixed("Wheel", 2));
        output.Show(new IndexEffect(), 1.0, EffectSpeed.Default);

        output.Render(TimeSpan.Zero);

        Assert.Equal([Led(0, 7), Led(1, 7), Led(2, 7), Led(3, 7), Led(4, 7), Led(5, 7), Led(6, 7)], output.Buffer);
    }

    // The longest zone gets the image; one LED takes the middle of it, two LEDs the middle of each
    // half.
    [Fact]
    public void Render_ZonesAlike_StretchesTheImageOfTheLongestZoneOverEveryZone()
    {
        var output = OutputFor(RgbZone.Fixed("Logo", 1), RgbZone.Fixed("Side", 4), RgbZone.Fixed("Wheel", 2));
        output.Show(new IndexEffect(), 1.0, EffectSpeed.Default, zonesAlike: true);

        output.Render(TimeSpan.Zero);

        Assert.Equal([Led(2, 4), Led(0, 4), Led(1, 4), Led(2, 4), Led(3, 4), Led(1, 4), Led(3, 4)], output.Buffer);
    }

    // As on a mouse with a primary and a logo zone of one LED each.
    [Fact]
    public void Render_ZonesAlike_TwoZonesOfOneLed_ShowOneColorOfTheRainbow()
    {
        var output = OutputFor(RgbZone.Fixed("Primary Zone", 1), RgbZone.Fixed("Logo Zone", 1));
        output.Show(new RainbowEffect(), 1.0, EffectSpeed.Default, zonesAlike: true);

        output.Render(TimeSpan.FromSeconds(1.3));

        var oneLed = new RgbColor[1];
        new RainbowEffect().Render(oneLed, TimeSpan.FromSeconds(1.3));
        Assert.Equal([oneLed[0], oneLed[0]], output.Buffer);
    }

    [Fact]
    public void Render_ZonesAlike_ZonesAsLong_GetTheSameImage()
    {
        var output = OutputFor(RgbZone.Fixed("Inner ring", 3), RgbZone.Fixed("Outer ring", 3));
        output.Show(new IndexEffect(), 1.0, EffectSpeed.Default, zonesAlike: true);

        output.Render(TimeSpan.Zero);

        Assert.Equal([Led(0, 3), Led(1, 3), Led(2, 3), Led(0, 3), Led(1, 3), Led(2, 3)], output.Buffer);
    }

    // The LEDs after the last zone belong to none, but show the image as well; a zone without LEDs
    // has nothing to show.
    [Fact]
    public void Render_ZonesAlike_TheLedsWithoutAZoneShowTheImageToo()
    {
        RgbZone[] zones = [new RgbZone("ARGB header", 0, 0, 60), RgbZone.Fixed("Mainboard", 2)];
        var output = new DeviceOutput(_provider, new RgbDevice("board", "Board", DeviceType.Motherboard, zones, 6) { Source = DeviceSource.Hardware });
        output.Show(new IndexEffect(), 1.0, EffectSpeed.Default, zonesAlike: true);

        output.Render(TimeSpan.Zero);

        Assert.Equal([Led(1, 4), Led(3, 4), Led(0, 4), Led(1, 4), Led(2, 4), Led(3, 4)], output.Buffer);
    }

    [Fact]
    public void Render_ZonesAlike_OneZone_RendersAsWithout()
    {
        var output = OutputFor(RgbZone.Fixed("Ring", 5));
        output.Show(new IndexEffect(), 1.0, EffectSpeed.Default, zonesAlike: true);

        output.Render(TimeSpan.Zero);

        Assert.Equal([Led(0, 5), Led(1, 5), Led(2, 5), Led(3, 5), Led(4, 5)], output.Buffer);
    }

    [Fact]
    public void Render_ZonesAlike_ScalesEveryZoneByTheBrightness()
    {
        var output = OutputFor(RgbZone.Fixed("Logo", 1), RgbZone.Fixed("Side", 4));
        output.Show(new IndexEffect(), 0.5, EffectSpeed.Default, zonesAlike: true);

        output.Render(TimeSpan.Zero);

        Assert.Equal([Led(2, 4).Scale(0.5), Led(0, 4).Scale(0.5), Led(1, 4).Scale(0.5), Led(2, 4).Scale(0.5), Led(3, 4).Scale(0.5)], output.Buffer);
    }

    // The flash goes over the image, so a zone of a device with zones alike still blinks alone.
    [Fact]
    public void Render_ZonesAlike_AFlashOfOneZoneGoesOverTheImage()
    {
        var output = OutputFor(RgbZone.Fixed("Logo", 1), RgbZone.Fixed("Side", 4));
        output.Show(new IndexEffect(), 1.0, EffectSpeed.Default, zonesAlike: true);
        output.StartFlash(TimeSpan.Zero, firstLed: 0, ledCount: 1);

        output.Render(TimeSpan.Zero);

        Assert.Equal([RgbColor.White, Led(0, 4), Led(1, 4), Led(2, 4), Led(3, 4)], output.Buffer);
    }

    [Fact]
    public void Show_WithoutZonesAlike_RendersOverTheWholeDeviceAgain()
    {
        var output = OutputFor(RgbZone.Fixed("Logo", 1), RgbZone.Fixed("Side", 2));
        output.Show(new IndexEffect(), 1.0, EffectSpeed.Default, zonesAlike: true);
        output.Render(TimeSpan.Zero);

        output.Show(new IndexEffect(), 1.0, EffectSpeed.Default);
        output.Render(TimeSpan.Zero);

        Assert.Equal([Led(0, 3), Led(1, 3), Led(2, 3)], output.Buffer);
    }

    private static RgbColor Led(int index, int length) => new((byte)index, (byte)length, 0);

    private DeviceOutput OutputFor(params RgbZone[] zones) =>
        new(_provider, new RgbDevice("device", "Device", DeviceType.Mouse, zones) { Source = DeviceSource.Hardware });

    // Each LED tells its index and the number of LEDs the effect got.
    private sealed class IndexEffect : IEffect
    {
        public bool IsAnimated => false;

        public void Render(Span<RgbColor> leds, TimeSpan time)
        {
            for (var i = 0; i < leds.Length; i++)
            {
                leds[i] = Led(i, leds.Length);
            }
        }
    }
}
