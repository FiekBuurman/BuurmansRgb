using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class SceneDevicesViewModelTests
{
    private static readonly DeviceItem Strip = new("strip", "LED strip", "LED-strip · 3 LED's", DeviceType.LedStrip);
    private static readonly DeviceItem Fan = new("fan", "Fan", "Ventilator · 2 LED's", DeviceType.Fan);

    private readonly SceneDevicesViewModel _devices = new();
    private readonly List<SceneDevicesChange> _changes = [];

    public SceneDevicesViewModelTests()
    {
        _devices.ApplyRequested += (_, change) => _changes.Add(change);
        _devices.ShowDevices([DeviceItem.AllDevices(2), Strip, Fan]);
    }

    // "Alle apparaten" in the device list is no device of a scene.
    [Fact]
    public void ShowDevices_LeavesOutAllDevices() =>
        Assert.Equal(["strip", "fan"], _devices.Options.Select(option => option.DeviceId));

    // The same devices keep their boxes, so the box that has the focus keeps it once its change is
    // saved; another device list gets new ones.
    [Fact]
    public void Show_SameDevices_KeepsTheBoxes()
    {
        _devices.Show("Lezen", ["fan"], isAllOff: false);
        var boxes = _devices.Options.ToList();

        _devices.Show("Lezen", ["strip"], isAllOff: false);
        Assert.Equal(boxes, _devices.Options);
        Assert.Equal([true, false], _devices.Options.Select(option => option.IsChecked));

        _devices.ShowDevices([Fan]);
        Assert.Equal(["fan"], _devices.Options.Select(option => option.DeviceId));
        Assert.DoesNotContain(_devices.Options, boxes.Contains);
        Assert.Empty(_changes);
    }

    // A box that goes off and on again before the engine answers sends both changes.
    [Fact]
    public void BoxOffAndOnAgain_SendsBoth()
    {
        _devices.Show("Lezen", ["strip", "fan"], isAllOff: false);

        _devices.Options[1].IsChecked = false;
        _devices.Options[1].IsChecked = true;

        Assert.Equal([["strip"], ["strip", "fan"]], _changes.Select(change => change.DeviceIds!.ToArray()));
    }

    // Without a scene there is nothing to choose for, and nothing is sent.
    [Fact]
    public void Clear_HasNoChoice()
    {
        _devices.Show("Lezen", ["fan"], isAllOff: false);

        _devices.Clear();
        _devices.AllDevices = false;

        Assert.False(_devices.CanChoose);
        Assert.Empty(_changes);
    }
}
