using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class DevicePreviewViewModelTests
{
    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly RgbColor Blue = new(0, 0, 255);

    // Six LEDs on the board, a 12 V header of one LED and an empty addressable header.
    private static readonly DeviceInfo Motherboard = new("board", "Motherboard", DeviceType.Motherboard, 7,
        [new RgbZone("Mainboard", 6, Resizable: false), new RgbZone("RGB header", 1, Resizable: false), new RgbZone("ARGB header", 0, Resizable: true)]);

    [Fact]
    public void Zones_FollowEachOtherInLedOrder()
    {
        var preview = new DevicePreviewViewModel(Motherboard);

        Assert.Equal(
            [("Mainboard", 0, 6), ("RGB header", 6, 1), ("ARGB header", 7, 0)],
            preview.Zones.Select(zone => (zone.Name, zone.FirstLed, zone.LedCount)));
        Assert.All(preview.Zones, zone => Assert.True(zone.ShowsHeader));
        Assert.Equal(["6 LED's", "1 LED", Strings.ZoneWithoutLedsResizable], preview.Zones.Select(zone => zone.Details));
        Assert.Equal("RGB header · 1 LED", preview.Zones[1].Header);
    }

    [Fact]
    public void Zones_DeviceWithOneZone_ShowsNoZoneHeader()
    {
        var preview = new DevicePreviewViewModel(TestDevices.Create("ram", "RAM stick", DeviceType.Ram, 8));

        var zone = Assert.Single(preview.Zones);
        Assert.False(zone.ShowsHeader);
        Assert.Equal(8, zone.Colors.Count);
    }

    [Fact]
    public void Zones_EmptyZoneThatCannotBeResized_SaysItHasNoLeds()
    {
        var device = new DeviceInfo("board", "Board", DeviceType.Motherboard, 0, [new RgbZone("Logo", 0, Resizable: false)]);

        var zone = Assert.Single(new DevicePreviewViewModel(device).Zones);

        Assert.True(zone.ShowsHeader);
        Assert.Equal(Strings.ZoneWithoutLeds, zone.Details);
    }

    [Fact]
    public void Zones_ClaimingMoreLedsThanTheDeviceHas_AreCutShort()
    {
        var device = new DeviceInfo("board", "Board", DeviceType.Motherboard, 3, [new RgbZone("A", 2, false), new RgbZone("B", 5, false)]);

        var preview = new DevicePreviewViewModel(device);

        Assert.Equal([(0, 2), (2, 1)], preview.Zones.Select(zone => (zone.FirstLed, zone.LedCount)));
    }

    [Fact]
    public void Zones_LedsWithoutZone_GetAZoneOfTheirOwn()
    {
        var device = new DeviceInfo("board", "Board", DeviceType.Motherboard, 5, [new RgbZone("A", 2, false)]);

        var preview = new DevicePreviewViewModel(device);

        Assert.Equal([("A", 0, 2), (Strings.ZoneOtherLeds, 2, 3)], preview.Zones.Select(zone => (zone.Name, zone.FirstLed, zone.LedCount)));
    }

    [Fact]
    public void Zones_DeviceWithoutZones_ShowsAllLedsInOneZone()
    {
        var device = new DeviceInfo("ram", "RAM stick", DeviceType.Ram, 4, []);

        var zone = Assert.Single(new DevicePreviewViewModel(device).Zones);

        Assert.Equal(("RAM stick", 0, 4), (zone.Name, zone.FirstLed, zone.LedCount));
        Assert.False(zone.ShowsHeader);
    }

    [Fact]
    public void Colors_AreShownPerZone()
    {
        var preview = new DevicePreviewViewModel(Motherboard);

        preview.Colors = [Red, Red, Red, Red, Red, Red, Blue];

        Assert.Equal(Enumerable.Repeat(Red, 6), preview.Zones[0].Colors);
        Assert.Equal([Blue], preview.Zones[1].Colors);
        Assert.Empty(preview.Zones[2].Colors);
    }

    [Fact]
    public void Colors_ShorterThanTheDevice_LeaveLaterZonesEmpty()
    {
        var preview = new DevicePreviewViewModel(Motherboard);

        preview.Colors = [Red, Red, Red];

        Assert.Equal([Red, Red, Red], preview.Zones[0].Colors);
        Assert.Empty(preview.Zones[1].Colors);
    }

    [Fact]
    public void ShowLedSelection_IsShownPerZoneFromTheStartOfTheZone()
    {
        var preview = new DevicePreviewViewModel(Motherboard);

        preview.ShowLedSelection(new LedSelection("board", "Motherboard", 4, 6));

        Assert.Equal((4, 6), preview.ShownSelection());
        Assert.Equal(
            [(4, 5), (0, 0), (-1, -1)],
            preview.Zones.Select(zone => (zone.SelectionFirst, zone.SelectionLast)));
    }

    [Fact]
    public void ShowLedSelection_OfAnotherDevice_ClearsEveryZone()
    {
        var preview = new DevicePreviewViewModel(Motherboard);
        preview.ShowLedSelection(new LedSelection("board", "Motherboard", 0, 6));

        preview.ShowLedSelection(new LedSelection("fan", "Fan", 0, 1));

        Assert.All(preview.Zones, zone => Assert.Equal((-1, -1), (zone.SelectionFirst, zone.SelectionLast)));
    }

    [Fact]
    public void ClickLed_InAZone_CountsFromTheStartOfTheDevice()
    {
        LedClick? clicked = null;
        var preview = new DevicePreviewViewModel(Motherboard, click => clicked = click);

        preview.Zones[1].ClickLedCommand.Execute(new LedHit(0, Extend: true));

        Assert.Equal(new LedClick("board", 6, Extend: true), clicked);
    }
}
