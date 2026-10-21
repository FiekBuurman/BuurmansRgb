namespace BuurmansRgb.Core.Tests;

public sealed class DeviceTypeTests
{
    // The engine and the settings app send the number over the pipe; one of an older build must
    // still mean the same type.
    [Theory]
    [InlineData(DeviceType.Motherboard, 0)]
    [InlineData(DeviceType.Headset, 8)]
    [InlineData(DeviceType.Other, 9)]
    [InlineData(DeviceType.Mousemat, 10)]
    [InlineData(DeviceType.Keypad, 15)]
    public void Number_StaysTheSame(DeviceType type, int number) => Assert.Equal(number, (int)type);

    [Fact]
    public void Numbers_AreUnique() =>
        Assert.Equal(Enum.GetValues<DeviceType>().Length, Enum.GetValues<DeviceType>().Select(type => (int)type).Distinct().Count());
}
