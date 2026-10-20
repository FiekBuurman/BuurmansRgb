using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

public sealed class DeviceIconsTests
{
    [Fact]
    public void For_MotherboardRamAndAllDevices_HaveTheirOwnIcon()
    {
        Assert.Same(DeviceIcons.Motherboard, DeviceIcons.For(DeviceType.Motherboard));
        Assert.Same(DeviceIcons.Ram, DeviceIcons.For(DeviceType.Ram));
        Assert.Same(DeviceIcons.AllDevices, DeviceIcons.For(null));
        Assert.Equal(4, new[] { DeviceIcons.Motherboard, DeviceIcons.Ram, DeviceIcons.AllDevices, DeviceIcons.Device }.Distinct().Count());
    }

    [Theory]
    [InlineData(DeviceType.Gpu)]
    [InlineData(DeviceType.Fan)]
    [InlineData(DeviceType.Other)]
    public void For_TypeWithoutIcon_GetsTheDeviceIcon(DeviceType type) => Assert.Same(DeviceIcons.Device, DeviceIcons.For(type));

    [Fact]
    public void Icons_FitTheIconSquare()
    {
        foreach (var icon in new[] { DeviceIcons.Motherboard, DeviceIcons.Ram, DeviceIcons.AllDevices, DeviceIcons.Device })
        {
            Assert.True(icon.IsFrozen);
            Assert.InRange(icon.Bounds.Left, 0, DeviceIcons.Size);
            Assert.InRange(icon.Bounds.Right, 0, DeviceIcons.Size);
            Assert.InRange(icon.Bounds.Top, 0, DeviceIcons.Size);
            Assert.InRange(icon.Bounds.Bottom, 0, DeviceIcons.Size);
        }
    }
}
