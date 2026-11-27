namespace BuurmansRgb.Core.Tests;

public sealed class DeviceErrorsTests
{
    [Fact]
    public void Unknown_NamesTheDeviceAndTheParameter()
    {
        var exception = DeviceErrors.Unknown("fan");

        Assert.Equal("deviceId", exception.ParamName);
        Assert.StartsWith("Unknown device 'fan'.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NotResizable_NamesTheZoneTheDeviceAndTheZoneIndex()
    {
        var exception = DeviceErrors.NotResizable(RgbZone.Fixed("Mainboard", 6), "board");

        Assert.Equal("zoneIndex", exception.ParamName);
        Assert.StartsWith("Zone 'Mainboard' of device 'board' cannot be resized.", exception.Message, StringComparison.Ordinal);
    }
}
