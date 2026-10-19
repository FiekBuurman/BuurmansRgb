using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts.Tests;

public sealed class DeviceFrameTests
{
    [Fact]
    public void FromColors_PacksThreeBytesPerLed()
    {
        var frame = DeviceFrame.FromColors("strip", [new RgbColor(1, 2, 3), new RgbColor(4, 5, 6)]);

        Assert.Equal("strip", frame.DeviceId);
        Assert.Equal([1, 2, 3, 4, 5, 6], frame.Leds);
    }

    [Fact]
    public void ToColors_RoundTripsFromColors()
    {
        RgbColor[] colors = [new(255, 136, 0), RgbColor.White, RgbColor.Black];

        Assert.Equal(colors, DeviceFrame.FromColors("fan", colors).ToColors());
    }

    [Fact]
    public void FromColors_NoLeds_IsEmpty()
    {
        var frame = DeviceFrame.FromColors("none", []);

        Assert.Empty(frame.Leds);
        Assert.Empty(frame.ToColors());
    }
}
