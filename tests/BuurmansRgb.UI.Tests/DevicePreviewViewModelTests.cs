using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class DevicePreviewViewModelTests
{
    // Six LEDs on the board, a 12 V header of one LED and an empty addressable header.
    private static readonly DeviceInfo Motherboard = new("board", "Motherboard", DeviceType.Motherboard, 7,
        [RgbZone.Fixed("Mainboard", 6), RgbZone.Fixed("RGB header", 1), new RgbZone("ARGB header", 0, 0, 120)]) { Source = DeviceSource.Hardware };

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
        Assert.Equal("Motherboard – RGB header", preview.Zones[1].AccessibleName);
    }

    [Fact]
    public void Zones_DeviceWithOneZone_ShowsNoZoneHeader()
    {
        var device = new DeviceInfo("ram", "ENE DRAM 2", DeviceType.Ram, 8, [RgbZone.Fixed("DRAM", 8)]) { Source = DeviceSource.Hardware };
        var preview = new DevicePreviewViewModel(device);

        var zone = Assert.Single(preview.Zones);
        Assert.False(zone.ShowsHeader);
        Assert.Equal("ENE DRAM 2", zone.AccessibleName);
        Assert.Equal(8, zone.Colors.Count);
    }

    // A keyboard with a key map and a logo beside it, as OpenRGB lists many keyboards.
    [Fact]
    public void Zones_KeyboardWithKeyMap_HandTheMatrixToTheView()
    {
        var keys = new LedMatrix(2, 2, [0, 1, 2, LedMatrix.NoLed]);
        var device = new DeviceInfo("keyboard", "Keyboard", DeviceType.Keyboard, 4,
            [RgbZone.Fixed("Keys", 3) with { Matrix = keys }, RgbZone.Fixed("Logo", 1)]) { Source = DeviceSource.Hardware };

        var preview = new DevicePreviewViewModel(device);

        Assert.Equal(keys, preview.Zones[0].Matrix);
        Assert.Null(preview.Zones[1].Matrix);
    }

    [Fact]
    public void Zones_EmptyZoneThatCannotBeResized_SaysItHasNoLeds()
    {
        var device = new DeviceInfo("board", "Board", DeviceType.Motherboard, 0, [RgbZone.Fixed("Logo", 0)]) { Source = DeviceSource.Hardware };

        var zone = Assert.Single(new DevicePreviewViewModel(device).Zones);

        Assert.True(zone.ShowsHeader);
        Assert.Equal(Strings.ZoneWithoutLeds, zone.Details);
        Assert.False(zone.ShowsEmptyZoneHint);
    }

    // An addressable header of which OpenRGB does not know the length: the user is told to fill in
    // the LEDs of the fan or strip on it.
    [Fact]
    public void Zones_EmptyZoneThatCanBeResized_ShowsTheHint()
    {
        var zones = new DevicePreviewViewModel(Motherboard).Zones;

        Assert.Equal([false, false, true], zones.Select(zone => zone.ShowsEmptyZoneHint));
        Assert.Equal("Nog geen LED's ingesteld", zones[2].Details);
        Assert.Contains("fan of strip", Strings.EmptyZoneHint);
        Assert.Contains(Strings.ApplyLedCount, Strings.EmptyZoneHint);
    }

    [Fact]
    public void Zones_ResizableZoneWithLeds_ShowsNoHint()
    {
        var strip = new DeviceInfo("strip", "LED strip", DeviceType.LedStrip, 30, [new RgbZone("Strip", 30, 1, 60)]) { Source = DeviceSource.Hardware };

        Assert.False(Assert.Single(new DevicePreviewViewModel(strip).Zones).ShowsEmptyZoneHint);
    }

    [Fact]
    public void Zones_ClaimingMoreLedsThanTheDeviceHas_AreCutShort()
    {
        var device = new DeviceInfo("board", "Board", DeviceType.Motherboard, 3, [RgbZone.Fixed("A", 2), RgbZone.Fixed("B", 5)]) { Source = DeviceSource.Hardware };

        var preview = new DevicePreviewViewModel(device);

        Assert.Equal([(0, 2), (2, 1)], preview.Zones.Select(zone => (zone.FirstLed, zone.LedCount)));
    }

    [Fact]
    public void Zones_LedsWithoutZone_GetAZoneOfTheirOwn()
    {
        var device = new DeviceInfo("board", "Board", DeviceType.Motherboard, 5, [RgbZone.Fixed("A", 2)]) { Source = DeviceSource.Hardware };

        var preview = new DevicePreviewViewModel(device);

        Assert.Equal([("A", 0, 2), (Strings.ZoneOtherLeds, 2, 3)], preview.Zones.Select(zone => (zone.Name, zone.FirstLed, zone.LedCount)));
    }

    [Fact]
    public void Zones_DeviceWithoutZones_ShowsAllLedsInOneZone()
    {
        var device = new DeviceInfo("ram", "RAM stick", DeviceType.Ram, 4, []) { Source = DeviceSource.Hardware };

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
        var actions = new RecordingPreviewActions();
        var preview = new DevicePreviewViewModel(Motherboard, actions);

        preview.Zones[1].ClickLedCommand.Execute(new LedHit(0, Extend: true));

        Assert.Equal([new LedClick("board", 6, Extend: true)], actions.Requests);
    }

    [Fact]
    public void Flash_AsksToFlashTheDevice()
    {
        var actions = new RecordingPreviewActions();
        var preview = new DevicePreviewViewModel(Motherboard, actions);

        preview.FlashCommand.Execute(null);

        Assert.Equal(["board"], actions.Requests);
        Assert.Equal("Motherboard laten knipperen", preview.FlashAccessibleName);
    }

    [Fact]
    public void ApplyLedCount_EmptyHeader_AsksForTheNewLength()
    {
        var actions = new RecordingPreviewActions();
        var header = new DevicePreviewViewModel(Motherboard, actions).Zones[2];

        Assert.True(header.CanResize);
        Assert.Equal("0", header.LedCountText);
        Assert.Equal("Aantal LED's (0–120):", header.LedCountLabel);
        Assert.Equal("Aantal LED's van Motherboard – ARGB header", header.LedCountAccessibleName);
        Assert.Equal("Aantal LED's toepassen voor Motherboard – ARGB header", header.ApplyLedCountAccessibleName);
        Assert.False(header.ApplyLedCountCommand.CanExecute(null));

        header.LedCountText = "30";
        Assert.True(header.ApplyLedCountCommand.CanExecute(null));
        header.ApplyLedCountCommand.Execute(null);

        Assert.Equal([new ZoneResize("board", 2, 30)], actions.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tien")]
    [InlineData("-1")]
    [InlineData("121")]
    [InlineData("2.5")]
    public void ApplyLedCount_NotALengthTheZoneCanHold_CannotRun(string text)
    {
        var header = new DevicePreviewViewModel(Motherboard).Zones[2];

        header.LedCountText = text;

        Assert.False(header.ApplyLedCountCommand.CanExecute(null));
    }

    [Fact]
    public void CanResize_FixedZonesAndLedsWithoutZone_CannotBeResized()
    {
        var device = new DeviceInfo("board", "Board", DeviceType.Motherboard, 5, [RgbZone.Fixed("A", 2)]) { Source = DeviceSource.Hardware };

        var zones = new DevicePreviewViewModel(device).Zones;

        Assert.All(zones, zone => Assert.False(zone.CanResize));
        Assert.All(new DevicePreviewViewModel(Motherboard).Zones.Take(2), zone => Assert.False(zone.CanResize));
    }

    // A strip with one zone shows no zone name, but its length can still be set.
    [Fact]
    public void CanResize_StripWithOneZone_WithoutHeader()
    {
        var strip = new DeviceInfo("strip", "LED strip", DeviceType.LedStrip, 30, [new RgbZone("Strip", 30, 1, 60)]) { Source = DeviceSource.Hardware };

        var zone = Assert.Single(new DevicePreviewViewModel(strip).Zones);

        Assert.False(zone.ShowsHeader);
        Assert.True(zone.CanResize);
        Assert.Equal("30", zone.LedCountText);
    }

    [Fact]
    public void SelectZone_RingOfAFan_HandsOnAllItsLeds()
    {
        var fan = new DeviceInfo("fan", "Dual-ring fan", DeviceType.Fan, 20, [RgbZone.Fixed("Inner ring", 8), RgbZone.Fixed("Outer ring", 12)]) { Source = DeviceSource.Hardware };
        var actions = new RecordingPreviewActions();
        var preview = new DevicePreviewViewModel(fan, actions);

        Assert.True(preview.Zones[1].ShowsHeaderLink);
        preview.Zones[1].SelectZoneCommand.Execute(null);

        Assert.Equal([new LedSelection("fan", "Dual-ring fan", 8, 19)], actions.Requests);
    }

    [Fact]
    public void SelectZone_ZoneWithoutLeds_CannotRunAndShowsPlainText()
    {
        var header = new DevicePreviewViewModel(Motherboard).Zones[2];

        Assert.False(header.SelectZoneCommand.CanExecute(null));
        Assert.False(header.ShowsHeaderLink);
        Assert.True(header.ShowsHeaderText);
    }
}
