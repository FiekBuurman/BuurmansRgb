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
}