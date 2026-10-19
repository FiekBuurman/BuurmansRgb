using BuurmansRgb.Core;
using OpenRgbDeviceType = OpenRGB.NET.DeviceType;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

public sealed class OpenRgbDeviceMapperTests
{
    // Motherboards and many graphics cards have fixed LEDs and an addressable header of their own.
    [Theory]
    [InlineData(OpenRgbDeviceType.Motherboard, DeviceType.Motherboard)]
    [InlineData(OpenRgbDeviceType.Gpu, DeviceType.Gpu)]
    public void Map_CopiesNameZonesAndLedCount(OpenRgbDeviceType type, DeviceType expectedType)
    {
        var data = TestDevices.Create(
            index: 3,
            name: "ASUS ROG STRIX",
            type: type,
            zones: [new("Aura LEDs", 4, 4, 4), new("Aura Addressable 1", 0, 0, 120)]);

        var mapped = Assert.Single(OpenRgbDeviceMapper.Map([data]));

        Assert.Equal(3, mapped.Index);
        Assert.Equal("ASUS ROG STRIX", mapped.Device.Name);
        Assert.Equal(expectedType, mapped.Device.Type);
        Assert.Equal(4, mapped.Device.LedCount);
        Assert.Equal(
            [new RgbZone("Aura LEDs", 4, Resizable: false), new RgbZone("Aura Addressable 1", 0, Resizable: true)],
            mapped.Device.Zones);
    }

    [Fact]
    public void Map_MotherboardWithOnlyEmptyHeaders_HasNoLedsButKeepsItsZones()
    {
        var data = TestDevices.Create(
            type: OpenRgbDeviceType.Motherboard,
            zones: [new("Addressable 1", 0, 0, 120), new("Addressable 2", 0, 0, 120)]);

        var mapped = Assert.Single(OpenRgbDeviceMapper.Map([data]));

        Assert.Equal(0, mapped.Device.LedCount);
        Assert.Equal(["Addressable 1", "Addressable 2"], mapped.Device.Zones.Select(zone => zone.Name));
        Assert.All(mapped.Device.Zones, zone => Assert.True(zone.Resizable));
    }

    // OpenRGB shows every RAM stick as a device of its own, all with the same name; the SMBus
    // address tells them apart.
    [Fact]
    public void Map_RamSticksOfTheSameModel_GetTheirOwnIdAndLedCount()
    {
        var sticks = Enumerable.Range(0, 4)
            .Select(slot => TestDevices.Create(
                index: slot,
                name: "ENE DRAM",
                type: OpenRgbDeviceType.Dram,
                location: $"I2C: PIIX4, address 0x{0x70 + slot:X2}",
                zones: [new("DRAM", 8, 8, 8)]))
            .ToArray();

        var mapped = OpenRgbDeviceMapper.Map(sticks);

        Assert.Equal(4, mapped.Select(item => item.Device.Id).Distinct().Count());
        Assert.All(mapped, item =>
        {
            Assert.Equal(DeviceType.Ram, item.Device.Type);
            Assert.Equal(8, item.Device.LedCount);
            Assert.DoesNotMatch("-[0-9]$", item.Device.Id);
        });
        Assert.Equal([0, 1, 2, 3], mapped.Select(item => item.Index));
        Assert.Equal(["ENE DRAM 1", "ENE DRAM 2", "ENE DRAM 3", "ENE DRAM 4"], mapped.Select(item => item.Device.Name));
    }

    // Two cards of the same model differ only in their PCI or SMBus location.
    [Fact]
    public void Map_TwoGpusOfTheSameModel_GetTheirOwnIdAndANumber()
    {
        var first = TestDevices.Create(index: 0, name: "MSI GeForce RTX 3070", type: OpenRgbDeviceType.Gpu,
            location: "I2C: NVIDIA GPU, address 0x68", ledCount: 1);
        var second = first with { Index = 1, Location = "I2C: NVIDIA GPU 1, address 0x68" };

        var mapped = OpenRgbDeviceMapper.Map([first, second]);

        Assert.Equal(["MSI GeForce RTX 3070 1", "MSI GeForce RTX 3070 2"], mapped.Select(item => item.Device.Name));
        Assert.Equal(
            [OpenRgbDeviceMapper.CreateId(first), OpenRgbDeviceMapper.CreateId(second)],
            mapped.Select(item => item.Device.Id));
        Assert.NotEqual(mapped[0].Device.Id, mapped[1].Device.Id);
        Assert.All(mapped, item => Assert.Equal((DeviceType.Gpu, 1), (item.Device.Type, item.Device.LedCount)));
    }

    [Fact]
    public void Map_DevicesWithTheirOwnName_KeepTheirName()
    {
        var mapped = OpenRgbDeviceMapper.Map(
        [
            TestDevices.Create(index: 0, name: "ASUS TUF GAMING B650-PLUS"),
            TestDevices.Create(index: 1, name: "ENE DRAM", location: "I2C: PIIX4, address 0x70"),
            TestDevices.Create(index: 2, name: "Corsair Vengeance Pro RGB"),
            TestDevices.Create(index: 3, name: "ENE DRAM", location: "I2C: PIIX4, address 0x71"),
        ]);

        Assert.Equal(
            ["ASUS TUF GAMING B650-PLUS", "ENE DRAM 1", "Corsair Vengeance Pro RGB", "ENE DRAM 2"],
            mapped.Select(item => item.Device.Name));
    }

    [Fact]
    public void Map_NumberedName_DoesNotChangeTheId()
    {
        var stick = TestDevices.Create(name: "ENE DRAM", location: "I2C: PIIX4, address 0x70");

        var mapped = OpenRgbDeviceMapper.Map([stick, stick with { Index = 1, Location = "I2C: PIIX4, address 0x71" }]);

        Assert.Equal(OpenRgbDeviceMapper.CreateId(stick), mapped[0].Device.Id);
    }

    [Theory]
    [InlineData(OpenRgbDeviceType.Motherboard, DeviceType.Motherboard)]
    [InlineData(OpenRgbDeviceType.Dram, DeviceType.Ram)]
    [InlineData(OpenRgbDeviceType.Gpu, DeviceType.Gpu)]
    [InlineData(OpenRgbDeviceType.Cooler, DeviceType.Fan)]
    [InlineData(OpenRgbDeviceType.Ledstrip, DeviceType.LedStrip)]
    [InlineData(OpenRgbDeviceMapper.CaseType, DeviceType.Case)]
    [InlineData(OpenRgbDeviceType.Keyboard, DeviceType.Keyboard)]
    [InlineData(OpenRgbDeviceType.Mouse, DeviceType.Mouse)]
    [InlineData(OpenRgbDeviceType.Headset, DeviceType.Headset)]
    [InlineData(OpenRgbDeviceType.Mousemat, DeviceType.Other)]
    [InlineData(OpenRgbDeviceType.Speaker, DeviceType.Other)]
    [InlineData(OpenRgbDeviceType.Unknown, DeviceType.Other)]
    [InlineData((OpenRgbDeviceType)99, DeviceType.Other)]
    public void ToDeviceType_MapsOpenRgbTypes(OpenRgbDeviceType type, DeviceType expected) =>
        Assert.Equal(expected, OpenRgbDeviceMapper.ToDeviceType(type));

    [Fact]
    public void CreateId_IsReadableAndStable()
    {
        var device = TestDevices.Create(name: "Corsair Vengeance Pro RGB", location: "I2C: PIIX4, address 0x58");

        var id = OpenRgbDeviceMapper.CreateId(device);

        Assert.Matches("^openrgb-corsair-vengeance-pro-rgb-[0-9a-f]{8}$", id);
        Assert.Equal(id, OpenRgbDeviceMapper.CreateId(device with { Index = 7 }));
    }

    [Fact]
    public void CreateId_SameModelOnOtherLocation_GetsOtherId()
    {
        var first = TestDevices.Create(name: "Corsair Vengeance Pro RGB", location: "I2C: PIIX4, address 0x58");
        var second = first with { Location = "I2C: PIIX4, address 0x59" };

        Assert.NotEqual(OpenRgbDeviceMapper.CreateId(first), OpenRgbDeviceMapper.CreateId(second));
    }

    [Fact]
    public void CreateId_NameWithoutLettersOrDigits_StillHasPrefixAndHash() =>
        Assert.Matches("^openrgb-[0-9a-f]{8}$", OpenRgbDeviceMapper.CreateId(TestDevices.Create(name: " ** ")));

    [Fact]
    public void CreateId_LongName_IsShortened()
    {
        var id = OpenRgbDeviceMapper.CreateId(TestDevices.Create(name: new string('a', 100)));

        Assert.Equal("openrgb-".Length + 40 + "-12345678".Length, id.Length);
    }

    [Fact]
    public void Map_IdenticalDevices_GetNumberedIds()
    {
        var device = TestDevices.Create(name: "Fan hub");

        var mapped = OpenRgbDeviceMapper.Map([device, device with { Index = 1 }, device with { Index = 2 }]);

        var id = OpenRgbDeviceMapper.CreateId(device);
        Assert.Equal([id, $"{id}-2", $"{id}-3"], mapped.Select(item => item.Device.Id));
    }

    [Theory]
    [InlineData("Direct")]
    [InlineData("Custom")]
    [InlineData("direct")]
    public void HasDirectMode_WithDirectOrCustomMode_IsTrue(string mode) =>
        Assert.True(OpenRgbDeviceMapper.HasDirectMode(TestDevices.Create(modes: ["Static", mode, "Breathing"])));

    [Fact]
    public void HasDirectMode_WithOnlyHardwareModes_IsFalse() =>
        Assert.False(OpenRgbDeviceMapper.HasDirectMode(TestDevices.Create(modes: ["Static", "Breathing"])));
}
