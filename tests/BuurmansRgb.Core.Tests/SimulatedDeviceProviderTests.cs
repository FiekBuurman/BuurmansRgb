using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Core.Tests;

public sealed class SimulatedDeviceProviderTests
{
    [Fact]
    public async Task DiscoverAsync_ReturnsStripFanAndKeyboardWithExpectedLedCounts()
    {
        var provider = new SimulatedDeviceProvider();

        var devices = await provider.DiscoverAsync(TestContext.Current.CancellationToken);

        Assert.Collection(
            devices,
            strip => AssertDevice(strip, DeviceType.LedStrip, 30),
            fan => AssertDevice(fan, DeviceType.Fan, 12),
            keyboard => AssertDevice(keyboard, DeviceType.Keyboard, 104));
    }

    [Fact]
    public async Task SetLeds_StoresColorsForDevice()
    {
        var provider = new SimulatedDeviceProvider();
        var devices = await provider.DiscoverAsync(TestContext.Current.CancellationToken);
        var fan = devices.Single(device => device.Type == DeviceType.Fan);
        var colors = Enumerable.Repeat(new RgbColor(10, 20, 30), fan.LedCount).ToArray();

        provider.SetLeds(fan.Id, colors);

        Assert.Equal(colors, provider.GetLeds(fan.Id).ToArray());
    }

    [Fact]
    public void SetLeds_WrongLedCount_Throws()
    {
        var provider = new SimulatedDeviceProvider();

        Assert.Throws<ArgumentException>(() => provider.SetLeds("simulated-fan", new RgbColor[3]));
    }

    private static void AssertDevice(RgbDevice device, DeviceType type, int ledCount)
    {
        Assert.Equal(type, device.Type);
        Assert.Equal(ledCount, device.LedCount);
        Assert.Equal(ledCount, device.Zones.Sum(zone => zone.LedCount));
    }
}
