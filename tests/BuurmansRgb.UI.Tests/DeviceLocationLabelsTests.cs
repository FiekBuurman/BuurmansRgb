using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class DeviceLocationLabelsTests
{
    // The RAM sticks of a kit sit on one SMBus; only their address differs.
    [Fact]
    public void For_RamSticksOfAKit_ShowOnlyTheirAddress()
    {
        var labels = DeviceLocationLabels.For(
        [
            Device("ram-1", "ENE DRAM 1", DeviceType.Ram, new("PIIX4 SMBus Adapter at 0B00", "0x70")),
            Device("ram-2", "ENE DRAM 2", DeviceType.Ram, new("PIIX4 SMBus Adapter at 0B00", "0x71")),
            Device("board", "ASUS TUF GAMING B650-PLUS", DeviceType.Motherboard, new("PIIX4 SMBus Adapter at 0B00", "0x4E")),
        ]);

        Assert.Equal(new Dictionary<string, string> { ["ram-1"] = "Adres 0x70", ["ram-2"] = "Adres 0x71" }, labels);
    }

    // Two cards of one model often have the same address, each on the bus of its own card.
    [Fact]
    public void For_TwoGpusWithTheSameAddress_ShowTheirBus()
    {
        var labels = DeviceLocationLabels.For(
        [
            Device("gpu-1", "MSI GeForce RTX 3070 1", DeviceType.Gpu, new("NVIDIA GPU", "0x68")),
            Device("gpu-2", "MSI GeForce RTX 3070 2", DeviceType.Gpu, new("NVIDIA GPU 1", "0x68")),
        ]);

        Assert.Equal("Op NVIDIA GPU", labels["gpu-1"]);
        Assert.Equal("Op NVIDIA GPU 1", labels["gpu-2"]);
    }

    [Fact]
    public void For_TwinsWithTheSameLocation_GetNoLabel()
    {
        var labels = DeviceLocationLabels.For(
        [
            Device("fan-1", "Fan 1", DeviceType.Fan, new("HID: hub")),
            Device("fan-2", "Fan 2", DeviceType.Fan, new("HID: hub")),
        ]);

        Assert.Empty(labels);
    }

    // Same name, but another type, or no location at all: nothing to tell apart.
    [Fact]
    public void For_NoTwins_GetNoLabel()
    {
        var labels = DeviceLocationLabels.For(
        [
            Device("ram", "Vengeance", DeviceType.Ram, new("PIIX4", "0x70")),
            Device("fan", "Vengeance", DeviceType.Fan, new("PIIX4", "0x71")),
            TestDevices.Create("strip-1", "Strip 1", DeviceType.LedStrip, 30),
            TestDevices.Create("strip-2", "Strip 2", DeviceType.LedStrip, 30),
        ]);

        Assert.Empty(labels);
    }

    private static DeviceInfo Device(string id, string name, DeviceType type, DeviceLocation location) =>
        TestDevices.Create(id, name, type, 8) with { Location = location };
}
