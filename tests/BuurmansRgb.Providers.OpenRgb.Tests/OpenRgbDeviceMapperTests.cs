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
        Assert.Equal(DeviceSource.Hardware, mapped.Device.Source);
        Assert.Equal(4, mapped.Device.LedCount);
        Assert.Equal(
            [RgbZone.Fixed("Aura LEDs", 4), new RgbZone("Aura Addressable 1", 0, 0, 120)],
            mapped.Device.Zones);
    }

    [Fact]
    public void Map_KeyNames_LoseTheirKeyPrefix()
    {
        var data = TestDevices.Create(type: OpenRgbDeviceType.Keyboard,
            zones: [new("Keys", 3, 3, 3, LedNames: ["Key: Escape", "Key: Number Pad 0", "Logo"]), new("Strip", 2, 2, 2, LedNames: ["LED 1", "LED 2"])]);

        var zones = Assert.Single(OpenRgbDeviceMapper.Map([data])).Device.Zones;

        Assert.Equal(["Escape", "Number Pad 0", "Logo"], zones[0].LedNames!);
        Assert.Equal(["LED 1", "LED 2"], zones[1].LedNames!);
    }

    // Only "Key: " at the start is OpenRGB's prefix.
    [Theory]
    [InlineData("Key:Escape")]
    [InlineData("key: Escape")]
    [InlineData("Left Key: 1")]
    public void ToLedNames_OtherNames_StayAsTheyAre(string name) =>
        Assert.Equal([name], OpenRgbDeviceMapper.ToLedNames([name])!);

    [Fact]
    public void ToLedNames_NoneOrOnlyEmptyNames_IsNoList()
    {
        Assert.Null(OpenRgbDeviceMapper.ToLedNames(null));
        Assert.Null(OpenRgbDeviceMapper.ToLedNames(["", " "]));
        Assert.Null(OpenRgbDeviceMapper.ToLedNames([]));
        Assert.Equal(["", "Logo"], OpenRgbDeviceMapper.ToLedNames(["", "Logo"])!);
    }

    // OpenRGB lists the keys of a keyboard in one zone, with a map of where each key sits.
    [Fact]
    public void Map_KeyboardWithKeyMap_KeepsTheKeyPositions()
    {
        var keys = new LedMatrix(2, 3, [0, 1, 2, 3, LedMatrix.NoLed, 4]);
        var data = TestDevices.Create(name: "Razer BlackWidow", type: OpenRgbDeviceType.Keyboard,
            zones: [new("Keyboard", 5, 5, 5, keys), new("Logo", 1, 1, 1)]);

        var mapped = Assert.Single(OpenRgbDeviceMapper.Map([data]));

        Assert.Equal(DeviceType.Keyboard, mapped.Device.Type);
        Assert.Equal(keys, mapped.Device.Zones[0].Matrix);
        Assert.Null(mapped.Device.Zones[1].Matrix);
    }

    // The preview turns those cells into gaps (LedLayout.Place); the mapper does not own that rule.
    [Fact]
    public void Map_KeyMapPointingPastTheZone_PassesTheMapOnAsItIs()
    {
        var keys = new LedMatrix(1, 4, [0, 3, 2, 7]);
        var data = TestDevices.Create(type: OpenRgbDeviceType.Keyboard,
            zones: [new("Keyboard", 3, 3, 3, keys), new("Logo", 1, 1, 1)]);

        var zone = Assert.Single(OpenRgbDeviceMapper.Map([data])).Device.Zones[0];

        Assert.Equal(keys, zone.Matrix);
    }

    [Fact]
    public void Map_EmptyKeyMap_HasNoMatrix()
    {
        var data = TestDevices.Create(zones: [new("Keyboard", 3, 3, 3, new LedMatrix(0, 0, []))]);

        Assert.Null(Assert.Single(OpenRgbDeviceMapper.Map([data])).Device.Zones[0].Matrix);
    }

    // Mice list their LEDs per zone, such as the logo, the scroll wheel and the sides.
    [Fact]
    public void Map_MouseWithZones_CopiesTheZones()
    {
        var data = TestDevices.Create(name: "Logitech G502 HERO", type: OpenRgbDeviceType.Mouse,
            zones: [new("Logo", 1, 1, 1), new("DPI", 1, 1, 1)]);

        var mapped = Assert.Single(OpenRgbDeviceMapper.Map([data]));

        Assert.Equal(DeviceType.Mouse, mapped.Device.Type);
        Assert.Equal([RgbZone.Fixed("Logo", 1), RgbZone.Fixed("DPI", 1)], mapped.Device.Zones);
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
        Assert.Equal(["0x70", "0x71", "0x72", "0x73"], mapped.Select(item => item.Device.Location?.Address));
        Assert.All(mapped, item => Assert.Equal("PIIX4", item.Device.Location?.Port));
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
        Assert.Equal(
            [new DeviceLocation("NVIDIA GPU", "0x68"), new DeviceLocation("NVIDIA GPU 1", "0x68")],
            mapped.Select(item => item.Device.Location));
    }

    [Theory]
    [InlineData("I2C: PIIX4 SMBus Adapter at 0B00, address 0x52", "PIIX4 SMBus Adapter at 0B00", "0x52")]
    [InlineData("I2C: Intel(R) SMBus - 7A23, address 0x5A", "Intel(R) SMBus - 7A23", "0x5A")]
    [InlineData(@"HID: \\?\hid#vid_0cf2&pid_a102", @"HID: \\?\hid#vid_0cf2&pid_a102", null)]
    [InlineData("I2C: 0x4E", "I2C: 0x4E", null)]
    public void ToLocation_SplitsAnSmBusAddressOffAndKeepsTheRest(string location, string port, string? address) =>
        Assert.Equal(new DeviceLocation(port, address), OpenRgbDeviceMapper.ToLocation(location));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ToLocation_Empty_IsUnknown(string location) => Assert.Null(OpenRgbDeviceMapper.ToLocation(location));

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
    [InlineData(OpenRgbDeviceType.Mousemat, DeviceType.Mousemat)]
    [InlineData(OpenRgbDeviceType.HeadsetStand, DeviceType.HeadsetStand)]
    [InlineData(OpenRgbDeviceType.Gamepad, DeviceType.Gamepad)]
    [InlineData(OpenRgbDeviceType.Speaker, DeviceType.Speaker)]
    [InlineData(OpenRgbDeviceType.Light, DeviceType.Other)]
    [InlineData(OpenRgbDeviceType.Virtual, DeviceType.Other)]
    [InlineData(OpenRgbDeviceMapper.MicrophoneType, DeviceType.Microphone)]
    [InlineData(OpenRgbDeviceMapper.KeypadType, DeviceType.Keypad)]
    [InlineData((OpenRgbDeviceType)17, DeviceType.Other)]
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

    [Fact]
    public void Map_PassesOnWhetherTheDeviceHasADirectMode()
    {
        var mapped = OpenRgbDeviceMapper.Map(
        [
            TestDevices.Create(index: 0, name: "Strip", modes: ["Direct"]),
            TestDevices.Create(index: 1, name: "Old GPU", modes: ["Static", "Breathing"]),
            TestDevices.Create(index: 2, name: "Keyboard", modes: ["Custom"]),
        ]);

        Assert.Equal([true, false, true], mapped.Select(item => item.Device.HasDirectMode));
    }
}
