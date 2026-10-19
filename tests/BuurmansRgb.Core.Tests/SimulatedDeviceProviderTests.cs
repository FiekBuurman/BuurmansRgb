using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Core.Tests;

public sealed class SimulatedDeviceProviderTests
{
    [Fact]
    public async Task DiscoverAsync_ReturnsEveryDeviceWithItsExpectedLedCount()
    {
        var provider = new SimulatedDeviceProvider();

        var devices = await provider.DiscoverAsync(TestContext.Current.CancellationToken);

        Assert.Collection(
            devices,
            strip => AssertDevice(strip, DeviceType.LedStrip, 30),
            fan => AssertDevice(fan, DeviceType.Fan, 12),
            keyboard => AssertDevice(keyboard, DeviceType.Keyboard, 104),
            motherboard => AssertDevice(motherboard, DeviceType.Motherboard, 7),
            ram => AssertDevice(ram, DeviceType.Ram, 8),
            ram => AssertDevice(ram, DeviceType.Ram, 8),
            gpu => AssertDevice(gpu, DeviceType.Gpu, 13),
            fan => AssertDevice(fan, DeviceType.Fan, 20),
            controller => AssertDevice(controller, DeviceType.LedStrip, 20),
            lighting => AssertDevice(lighting, DeviceType.Case, 10));
    }

    [Fact]
    public async Task DiscoverAsync_DualRingFanHasARingPerZone()
    {
        var provider = new SimulatedDeviceProvider();

        var devices = await provider.DiscoverAsync(TestContext.Current.CancellationToken);

        var fan = devices.Single(device => device.Id == "simulated-fan-dual");
        Assert.Equal([RgbZone.Fixed("Inner ring", 8), RgbZone.Fixed("Outer ring", 12)], fan.Zones);
    }

    [Fact]
    public async Task DiscoverAsync_ArgbControllerHasTwoResizableChannels()
    {
        var provider = new SimulatedDeviceProvider();

        var devices = await provider.DiscoverAsync(TestContext.Current.CancellationToken);

        var controller = devices.Single(device => device.Id == "simulated-argb");
        Assert.Equal([new RgbZone("ARGB 1", 20, 0, 60), new RgbZone("ARGB 2", 0, 0, 60)], controller.Zones);
    }

    [Fact]
    public async Task DiscoverAsync_GpuHasALightBarAndALogo()
    {
        var provider = new SimulatedDeviceProvider();

        var devices = await provider.DiscoverAsync(TestContext.Current.CancellationToken);

        var gpu = devices.Single(device => device.Type == DeviceType.Gpu);
        Assert.Equal([RgbZone.Fixed("Light bar", 12), RgbZone.Fixed("Logo", 1)], gpu.Zones);
    }

    [Fact]
    public async Task DiscoverAsync_MotherboardHasAnEmptyResizableHeader()
    {
        var provider = new SimulatedDeviceProvider();

        var devices = await provider.DiscoverAsync(TestContext.Current.CancellationToken);

        var motherboard = devices.Single(device => device.Type == DeviceType.Motherboard);
        Assert.Equal(
            [RgbZone.Fixed("Mainboard", 6), RgbZone.Fixed("RGB header", 1), new RgbZone("ARGB header", 0, 0, 120)],
            motherboard.Zones);
    }

    [Fact]
    public async Task SetLeds_StoresColorsForDevice()
    {
        var provider = new SimulatedDeviceProvider();
        var devices = await provider.DiscoverAsync(TestContext.Current.CancellationToken);
        var fan = devices.Single(device => device.Id == "simulated-fan");
        var colors = Enumerable.Repeat(new RgbColor(10, 20, 30), fan.LedCount).ToArray();

        provider.SetLeds(fan.Id, colors);

        Assert.Equal(colors, provider.GetLeds(fan.Id).ToArray());
    }

    [Fact]
    public void SetLeds_WrongLedCount_IsSkipped()
    {
        var provider = new SimulatedDeviceProvider();

        provider.SetLeds("simulated-fan", Enumerable.Repeat(RgbColor.White, 3).ToArray());

        Assert.All(provider.GetLeds("simulated-fan").ToArray(), led => Assert.Equal(RgbColor.Black, led));
    }

    [Fact]
    public void SetLeds_UnknownDevice_Throws()
    {
        var provider = new SimulatedDeviceProvider();

        Assert.Throws<ArgumentException>(() => provider.SetLeds("unknown", new RgbColor[3]));
    }

    [Fact]
    public async Task ResizeZone_ChangesTheZoneAndRaisesDevicesChanged()
    {
        var provider = new SimulatedDeviceProvider();
        var changed = 0;
        provider.DevicesChanged += (_, _) => changed++;

        provider.ResizeZone("simulated-motherboard", 2, 20);

        var devices = await provider.DiscoverAsync(TestContext.Current.CancellationToken);
        var motherboard = devices.Single(device => device.Id == "simulated-motherboard");
        Assert.Equal(1, changed);
        Assert.Equal(new RgbZone("ARGB header", 20, 0, 120), motherboard.Zones[2]);
        Assert.Equal(27, motherboard.LedCount);
        Assert.Equal(27, provider.GetLeds("simulated-motherboard").Length);
    }

    [Fact]
    public void ResizeZone_ThenSetLeds_TakesTheNewLength()
    {
        var provider = new SimulatedDeviceProvider();
        provider.ResizeZone("simulated-strip", 0, 40);

        provider.SetLeds("simulated-strip", Enumerable.Repeat(RgbColor.White, 40).ToArray());

        Assert.All(provider.GetLeds("simulated-strip").ToArray(), led => Assert.Equal(RgbColor.White, led));
    }

    [Fact]
    public void ResizeZone_SameLength_RaisesNothing()
    {
        var provider = new SimulatedDeviceProvider();
        var changed = 0;
        provider.DevicesChanged += (_, _) => changed++;

        provider.ResizeZone("simulated-strip", 0, 30);

        Assert.Equal(0, changed);
    }

    [Theory]
    [InlineData("simulated-strip", 0, 0)]
    [InlineData("simulated-strip", 0, 61)]
    [InlineData("simulated-strip", 1, 10)]
    [InlineData("simulated-fan", 0, 16)]
    public void ResizeZone_OutsideTheRangeOrOfAFixedZone_Throws(string deviceId, int zoneIndex, int ledCount)
    {
        var provider = new SimulatedDeviceProvider();

        Assert.Throws<ArgumentOutOfRangeException>(() => provider.ResizeZone(deviceId, zoneIndex, ledCount));
    }

    private static void AssertDevice(RgbDevice device, DeviceType type, int ledCount)
    {
        Assert.Equal(type, device.Type);
        Assert.Equal(ledCount, device.LedCount);
        Assert.Equal(ledCount, device.Zones.Sum(zone => zone.LedCount));
    }
}
