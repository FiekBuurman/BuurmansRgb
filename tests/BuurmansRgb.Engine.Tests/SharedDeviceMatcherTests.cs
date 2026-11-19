using BuurmansRgb.Core;
using BuurmansRgb.Engine.Sharing;

namespace BuurmansRgb.Engine.Tests;

public sealed class SharedDeviceMatcherTests
{
    private static readonly RgbDevice Ram1 = Device("openrgb:ram-aaaa", "Vengeance", DeviceType.Ram);
    private static readonly RgbDevice Ram2 = Device("openrgb:ram-bbbb", "Vengeance", DeviceType.Ram);
    private static readonly RgbDevice Fan = Device("openrgb:fan-cccc", "Fan", DeviceType.Fan);

    [Fact]
    public void Match_SameId_IsThatDevice()
    {
        var (map, unmatched) = SharedDeviceMatcher.Match([new("openrgb:fan-cccc", DeviceType.Fan, "Andere naam")], [Fan]);

        Assert.Equal("openrgb:fan-cccc", map["openrgb:fan-cccc"]);
        Assert.Empty(unmatched);
    }

    // Two of the same type and name go in their order; a third finds none left.
    [Fact]
    public void Match_TwoAlike_GoInTheirOrder()
    {
        var (map, unmatched) = SharedDeviceMatcher.Match(
            [new("other:ram-1", DeviceType.Ram, "Vengeance"), new("other:ram-2", DeviceType.Ram, "Vengeance"), new("other:ram-3", DeviceType.Ram, "Vengeance")],
            [Fan, Ram1, Ram2]);

        Assert.Equal(Ram1.Id, map["other:ram-1"]);
        Assert.Equal(Ram2.Id, map["other:ram-2"]);
        Assert.Equal(["other:ram-3"], unmatched.Select(device => device.Id));
    }

    // A device that is found by its id is not given to another by type and name.
    [Fact]
    public void Match_ById_GoesFirst()
    {
        var (map, _) = SharedDeviceMatcher.Match(
            [new("other:ram-1", DeviceType.Ram, "Vengeance"), new(Ram1.Id, DeviceType.Ram, "Vengeance")],
            [Ram1, Ram2]);

        Assert.Equal(Ram1.Id, map[Ram1.Id]);
        Assert.Equal(Ram2.Id, map["other:ram-1"]);
    }

    // The same name of another type, or a device without type or name, is not the one.
    [Fact]
    public void Match_OtherTypeOrUnknown_IsNotHere()
    {
        var (map, unmatched) = SharedDeviceMatcher.Match(
            [new("other:fan", DeviceType.Gpu, "Fan"), new("other:x", null, null), new("other:y", DeviceType.Fan, null)],
            [Fan]);

        Assert.Empty(map);
        Assert.Equal(3, unmatched.Count);
    }

    private static RgbDevice Device(string id, string name, DeviceType type) =>
        new(id, name, type, [RgbZone.Fixed("Zone", 8)]) { Source = DeviceSource.Hardware };
}
