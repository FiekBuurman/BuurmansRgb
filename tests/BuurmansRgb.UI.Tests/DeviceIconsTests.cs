using System.Windows.Media;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Views;

namespace BuurmansRgb.UI.Tests;

public sealed class DeviceIconsTests
{
    private static readonly Geometry[] AllIcons =
    [
        DeviceIcons.Motherboard, DeviceIcons.Ram, DeviceIcons.Gpu, DeviceIcons.Fan, DeviceIcons.LedStrip, DeviceIcons.Case,
        DeviceIcons.Keyboard, DeviceIcons.Mouse, DeviceIcons.Headset, DeviceIcons.Mousemat, DeviceIcons.HeadsetStand, DeviceIcons.Speaker,
        DeviceIcons.Gamepad, DeviceIcons.AllDevices, DeviceIcons.Device,
    ];

    [Fact]
    public void For_TypesWithAnIconAndAllDevices_HaveTheirOwnIcon()
    {
        Assert.Same(DeviceIcons.Motherboard, DeviceIcons.For(DeviceType.Motherboard));
        Assert.Same(DeviceIcons.Ram, DeviceIcons.For(DeviceType.Ram));
        Assert.Same(DeviceIcons.Gpu, DeviceIcons.For(DeviceType.Gpu));
        Assert.Same(DeviceIcons.Fan, DeviceIcons.For(DeviceType.Fan));
        Assert.Same(DeviceIcons.LedStrip, DeviceIcons.For(DeviceType.LedStrip));
        Assert.Same(DeviceIcons.Case, DeviceIcons.For(DeviceType.Case));
        Assert.Same(DeviceIcons.Keyboard, DeviceIcons.For(DeviceType.Keyboard));
        Assert.Same(DeviceIcons.Mouse, DeviceIcons.For(DeviceType.Mouse));
        Assert.Same(DeviceIcons.Headset, DeviceIcons.For(DeviceType.Headset));
        Assert.Same(DeviceIcons.Mousemat, DeviceIcons.For(DeviceType.Mousemat));
        Assert.Same(DeviceIcons.HeadsetStand, DeviceIcons.For(DeviceType.HeadsetStand));
        Assert.Same(DeviceIcons.Speaker, DeviceIcons.For(DeviceType.Speaker));
        Assert.Same(DeviceIcons.Gamepad, DeviceIcons.For(DeviceType.Gamepad));
        Assert.Same(DeviceIcons.AllDevices, DeviceIcons.For(null));
        Assert.Equal(AllIcons.Length, AllIcons.Distinct().Count());
    }

    [Fact]
    public void For_TypeWithoutIcon_GetsTheDeviceIcon() => Assert.Same(DeviceIcons.Device, DeviceIcons.For(DeviceType.Other));

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
