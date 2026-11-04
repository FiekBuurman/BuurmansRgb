using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.Simulated.Tests;

public sealed class SimulatedDeviceProviderTests
{
    [Fact]
    public async Task DiscoverAsync_ReturnsEveryDeviceWithItsExpectedLedCount()
    {
        var provider = new SimulatedDeviceProvider();

        var devices = await provider.DiscoverAsync(Ct);

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
            lighting => AssertDevice(lighting, DeviceType.Case, 10),
            mouse => AssertDevice(mouse, DeviceType.Mouse, 12),
            headset => AssertDevice(headset, DeviceType.Headset, 2));
    }

    // The simulated devices never go away, so a search finds them all again and keeps their lengths.
    [Fact]
    public async Task RequestRescanAsync_ReportsTheSameDevicesAgain()
    {
        var provider = new SimulatedDeviceProvider();
        provider.ResizeZone("simulated-strip", 0, 40);
        var before = await provider.DiscoverAsync(Ct);
        var changes = 0;
        provider.DevicesChanged += (_, _) => changes++;

        Assert.True(await provider.RequestRescanAsync(Ct));

        Assert.Equal(1, changes);
        Assert.Equal(before, await provider.DiscoverAsync(Ct));
    }

    // Shown in the settings app, so Dutch like the rest of it, and each one its own.
    [Fact]
    public async Task DiscoverAsync_EveryDeviceHasItsOwnDutchName()
    {
        var provider = new SimulatedDeviceProvider();

        var names = (await provider.DiscoverAsync(Ct)).Select(device => device.Name).ToList();

        Assert.All(names, name => Assert.StartsWith("Gesimuleerd", name, StringComparison.Ordinal));
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.Contains("Gesimuleerd toetsenbord", names);
    }

    [Fact]
    public async Task DiscoverAsync_KeyboardHasAKeyMapWithEveryKeyOnce()
    {
        var provider = new SimulatedDeviceProvider();

        var devices = await provider.DiscoverAsync(Ct);

        var keys = Assert.Single(devices.Single(device => device.Type == DeviceType.Keyboard).Zones);
        var matrix = Assert.IsType<LedMatrix>(keys.Matrix);
        Assert.Equal((6, 24), (matrix.Height, matrix.Width));
        Assert.Equal(Enumerable.Range(0, keys.LedCount), matrix.Cells.Where(cell => cell != LedMatrix.NoLed).Order());

        // Esc on the far left of the top row, the 0 of the number pad at the bottom.
        Assert.Equal(0, matrix[0, 0]);
        Assert.Equal(LedMatrix.NoLed, matrix[0, 1]);
        Assert.Equal(102, matrix[5, 20]);
    }

    // Every key has a name, and the names follow the key map: each key sits where its name says.
    [Theory]
    [InlineData(0, 0, "Escape")]
    [InlineData(0, 16, "Print Screen")]
    [InlineData(1, 13, "Backspace")]
    [InlineData(1, 23, "Number Pad -")]
    [InlineData(2, 13, "\\")]
    [InlineData(3, 0, "Caps Lock")]
    [InlineData(3, 13, "Enter")]
    [InlineData(4, 13, "Right Shift")]
    [InlineData(4, 17, "Up Arrow")]
    [InlineData(4, 23, "Number Pad Enter")]
    [InlineData(5, 6, "Space")]
    [InlineData(5, 13, "Right Control")]
    [InlineData(5, 22, "Number Pad .")]
    public async Task DiscoverAsync_KeyboardKeysHaveTheNameOfTheirPlace(int row, int column, string name)
    {
        var devices = await new SimulatedDeviceProvider().DiscoverAsync(Ct);

        var keys = Assert.Single(devices.Single(device => device.Type == DeviceType.Keyboard).Zones);
        var names = Assert.IsAssignableFrom<IReadOnlyList<string>>(keys.LedNames);
        Assert.Equal(keys.LedCount, names.Count);
        Assert.Equal(keys.LedCount, names.Distinct().Count());
        Assert.Equal(name, names[keys.Matrix![row, column]]);
    }

    // Only the keyboard copies a real layout; the other devices have no names to give.
    [Fact]
    public async Task DiscoverAsync_OtherDevices_HaveNoLedNames()
    {
        var devices = await new SimulatedDeviceProvider().DiscoverAsync(Ct);

        Assert.All(devices.Where(device => device.Type != DeviceType.Keyboard).SelectMany(device => device.Zones), zone => Assert.Null(zone.LedNames));
    }

    [Fact]
    public async Task DiscoverAsync_MouseAndHeadsetHaveTheirZones()
    {
        var provider = new SimulatedDeviceProvider();

        var devices = await provider.DiscoverAsync(Ct);

        Assert.Equal(["Logo", "Scrollwiel", "Linkerkant", "Rechterkant"],
            devices.Single(device => device.Type == DeviceType.Mouse).Zones.Select(zone => zone.Name));
        Assert.Equal(["Linkeroorschelp", "Rechteroorschelp"],
            devices.Single(device => device.Type == DeviceType.Headset).Zones.Select(zone => zone.Name));
    }

    [Fact]
    public async Task DiscoverAsync_DualRingFanHasARingPerZone()
    {
        var provider = new SimulatedDeviceProvider();

        var devices = await provider.DiscoverAsync(Ct);

        var fan = devices.Single(device => device.Id == "simulated-fan-dual");
        Assert.Equal([Fixed("Inner ring", "Binnenring", 8), Fixed("Outer ring", "Buitenring", 12)], fan.Zones);
    }

    [Fact]
    public async Task DiscoverAsync_ArgbControllerHasTwoResizableChannels()
    {
        var provider = new SimulatedDeviceProvider();

        var devices = await provider.DiscoverAsync(Ct);

        var controller = devices.Single(device => device.Id == "simulated-argb");
        Assert.Equal([Resizable("ARGB 1", "Kanaal 1", 20, 0, 60), Resizable("ARGB 2", "Kanaal 2", 0, 0, 60)], controller.Zones);
    }

    [Fact]
    public async Task DiscoverAsync_GpuHasALightBarAndALogo()
    {
        var provider = new SimulatedDeviceProvider();

        var devices = await provider.DiscoverAsync(Ct);

        var gpu = devices.Single(device => device.Type == DeviceType.Gpu);
        Assert.Equal([Fixed("Light bar", "Lichtbalk", 12), Fixed("Logo", "Logo", 1)], gpu.Zones);
    }

    [Fact]
    public async Task DiscoverAsync_MotherboardHasAnEmptyResizableHeader()
    {
        var provider = new SimulatedDeviceProvider();

        var devices = await provider.DiscoverAsync(Ct);

        var motherboard = devices.Single(device => device.Type == DeviceType.Motherboard);
        Assert.Equal(
            [Fixed("Mainboard", "Moederbord", 6), Fixed("RGB header", "RGB-aansluiting", 1), Resizable("ARGB header", "ARGB-aansluiting", 0, 0, 120)],
            motherboard.Zones);
    }

    [Fact]
    public async Task SetLeds_StoresColorsForDevice()
    {
        var provider = new SimulatedDeviceProvider();
        var devices = await provider.DiscoverAsync(Ct);
        var fan = devices.Single(device => device.Id == "simulated-fan");
        var colors = Enumerable.Repeat(new RgbColor(10, 20, 30), fan.LedCount).ToArray();

        provider.SetLeds(fan.Id, colors);

        Assert.Equal(colors, provider.GetLeds(fan.Id));
    }

    [Fact]
    public void SetLeds_WrongLedCount_IsSkipped()
    {
        var provider = new SimulatedDeviceProvider();

        provider.SetLeds("simulated-fan", Enumerable.Repeat(RgbColor.White, 3).ToArray());

        Assert.All(provider.GetLeds("simulated-fan"), led => Assert.Equal(RgbColor.Black, led));
    }

    [Fact]
    public void SetLeds_UnknownDevice_IsIgnored()
    {
        var provider = new SimulatedDeviceProvider();

        provider.SetLeds("unknown", new RgbColor[3]);

        Assert.Throws<ArgumentException>(() => provider.GetLeds("unknown"));
    }

    [Fact]
    public async Task ResizeZone_ChangesTheZoneAndRaisesDevicesChanged()
    {
        var provider = new SimulatedDeviceProvider();
        var changed = 0;
        provider.DevicesChanged += (_, _) => changed++;

        provider.ResizeZone("simulated-motherboard", 2, 20);

        var devices = await provider.DiscoverAsync(Ct);
        var motherboard = devices.Single(device => device.Id == "simulated-motherboard");
        Assert.Equal(1, changed);
        Assert.Equal(Resizable("ARGB header", "ARGB-aansluiting", 20, 0, 120), motherboard.Zones[2]);
        Assert.Equal(27, motherboard.LedCount);
        Assert.Equal(DeviceSource.Simulated, motherboard.Source);
        Assert.Equal(27, provider.GetLeds("simulated-motherboard").Length);
    }

    [Fact]
    public void ResizeZone_ThenSetLeds_TakesTheNewLength()
    {
        var provider = new SimulatedDeviceProvider();
        provider.ResizeZone("simulated-strip", 0, 40);

        provider.SetLeds("simulated-strip", Enumerable.Repeat(RgbColor.White, 40).ToArray());

        Assert.All(provider.GetLeds("simulated-strip"), led => Assert.Equal(RgbColor.White, led));
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

    // settings.json keeps the length of a zone under the English name the zone had before it got a
    // Dutch one (zoneLengths), so every zone keeps that name as its id.
    [Fact]
    public async Task DiscoverAsync_EveryZoneHasADutchNameAndItsEnglishNameAsId()
    {
        var devices = await new SimulatedDeviceProvider().DiscoverAsync(Ct);

        Assert.Equal(
            [
                ("simulated-strip", "Strip", "Strip"),
                ("simulated-fan", "Ring", "Ring"),
                ("simulated-keyboard", "Keys", "Toetsen"),
                ("simulated-motherboard", "Mainboard", "Moederbord"),
                ("simulated-motherboard", "RGB header", "RGB-aansluiting"),
                ("simulated-motherboard", "ARGB header", "ARGB-aansluiting"),
                ("simulated-ram-1", "DRAM", "Geheugen"),
                ("simulated-ram-2", "DRAM", "Geheugen"),
                ("simulated-gpu", "Light bar", "Lichtbalk"),
                ("simulated-gpu", "Logo", "Logo"),
                ("simulated-fan-dual", "Inner ring", "Binnenring"),
                ("simulated-fan-dual", "Outer ring", "Buitenring"),
                ("simulated-argb", "ARGB 1", "Kanaal 1"),
                ("simulated-argb", "ARGB 2", "Kanaal 2"),
                ("simulated-case", "Front panel", "Voorpaneel"),
                ("simulated-mouse", "Logo", "Logo"),
                ("simulated-mouse", "Scroll wheel", "Scrollwiel"),
                ("simulated-mouse", "Left side", "Linkerkant"),
                ("simulated-mouse", "Right side", "Rechterkant"),
                ("simulated-headset", "Left ear cup", "Linkeroorschelp"),
                ("simulated-headset", "Right ear cup", "Rechteroorschelp"),
            ],
            devices.SelectMany(device => device.Zones.Select(zone => (device.Id, zone.Key, zone.Name))));
    }

    private static RgbZone Fixed(string id, string name, int ledCount) => RgbZone.Fixed(name, ledCount) with { Id = id };

    private static RgbZone Resizable(string id, string name, int ledCount, int minLedCount, int maxLedCount) =>
        new(name, ledCount, minLedCount, maxLedCount) { Id = id };

    private static void AssertDevice(RgbDevice device, DeviceType type, int ledCount)
    {
        Assert.Equal(type, device.Type);
        Assert.Equal(DeviceSource.Simulated, device.Source);
        Assert.Equal(ledCount, device.LedCount);
        Assert.Equal(ledCount, device.Zones.Sum(zone => zone.LedCount));
    }
}
