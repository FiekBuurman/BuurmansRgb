using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

public sealed class DeviceIconsTests
{
    private static readonly Geometry[] AllIcons =
        [DeviceIcons.Motherboard, DeviceIcons.Ram, DeviceIcons.Gpu, DeviceIcons.AllDevices, DeviceIcons.Device];

    [Fact]
    public void For_TypesWithAnIconAndAllDevices_HaveTheirOwnIcon()
    {
        Assert.Same(DeviceIcons.Motherboard, DeviceIcons.For(DeviceType.Motherboard));
        Assert.Same(DeviceIcons.Ram, DeviceIcons.For(DeviceType.Ram));
        Assert.Same(DeviceIcons.Gpu, DeviceIcons.For(DeviceType.Gpu));
        Assert.Same(DeviceIcons.AllDevices, DeviceIcons.For(null));
        Assert.Equal(AllIcons.Length, AllIcons.Distinct().Count());
    }

    [Theory]
    [InlineData(DeviceType.Fan)]
    [InlineData(DeviceType.Keyboard)]
    [InlineData(DeviceType.Other)]
    public void For_TypeWithoutIcon_GetsTheDeviceIcon(DeviceType type) => Assert.Same(DeviceIcons.Device, DeviceIcons.For(type));

    [Fact]
    public void Icons_FitTheIconSquare()
    {
        foreach (var icon in AllIcons)
        {
            Assert.True(icon.IsFrozen);
            Assert.InRange(icon.Bounds.Left, 0, DeviceIcons.Size);
            Assert.InRange(icon.Bounds.Right, 0, DeviceIcons.Size);
            Assert.InRange(icon.Bounds.Top, 0, DeviceIcons.Size);
            Assert.InRange(icon.Bounds.Bottom, 0, DeviceIcons.Size);
        }
    }
}
