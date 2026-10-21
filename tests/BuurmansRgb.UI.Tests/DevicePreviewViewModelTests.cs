using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;

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

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void LacksDirectMode_FollowsTheDevice(bool hasDirectMode, bool lacksDirectMode)
    {
        var preview = new DevicePreviewViewModel(Motherboard with { HasDirectMode = hasDirectMode });

        Assert.Equal(lacksDirectMode, preview.LacksDirectMode);
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

    // The number counts from the start of the device, as the per-LED editor says "LED 4".
    [Fact]
    public void LedLabels_GiveTheNameAndTheNumberInTheDevice()
    {
        var device = new DeviceInfo("keyboard", "Keyboard", DeviceType.Keyboard, 4,
            [RgbZone.Fixed("Keys", 3) with { LedNames = ["Escape", " ", "F1"] }, RgbZone.Fixed("Logo", 1)])
        {
            Source = DeviceSource.Hardware,
        };

        var zones = new DevicePreviewViewModel(device).Zones;

        Assert.Equal(["Escape (LED 1)", "LED 2", "F1 (LED 3)"], zones[0].LedLabels);
        Assert.Equal(["LED 4"], zones[1].LedLabels);
    }

    // The list of names may be shorter or longer than the zone.
    [Fact]
    public void LedLabels_NamesThatDoNotMatchTheZone_StillGiveALabelPerLed()
    {
        var device = new DeviceInfo("strip", "Strip", DeviceType.LedStrip, 5,
            [RgbZone.Fixed("Short", 2) with { LedNames = ["A"] }, RgbZone.Fixed("Long", 3) with { LedNames = ["B", "C", "D", "E"] }])
        {
            Source = DeviceSource.Hardware,
        };

        var zones = new DevicePreviewViewModel(device).Zones;

        Assert.Equal(["A (LED 1)", "LED 2"], zones[0].LedLabels);
        Assert.Equal(["B (LED 3)", "C (LED 4)", "D (LED 5)"], zones[1].LedLabels);
    }

    // A dual-ring fan: each ring is a zone of its own and shows as a ring of its own, up to the
    // largest fan ring.
    [Fact]
    public void Zones_OfAFan_AreRings()
    {
        var fan = new DeviceInfo("fan", "Fan", DeviceType.Fan, 20 + ZonePreviewViewModel.MaxRingLeds,
            [RgbZone.Fixed("Inner ring", 8), RgbZone.Fixed("Outer ring", 12), RgbZone.Fixed("Big ring", ZonePreviewViewModel.MaxRingLeds)])
        {
            Source = DeviceSource.Hardware,
        };

        Assert.All(new DevicePreviewViewModel(fan).Zones, zone => Assert.True(zone.IsRing));
    }

    // OpenRGB calls a hub such as a Lian Li Uni Hub a cooler too; a channel of it can hold a strip
    // or several fans in a row, and a zone longer than a fan ring is not one ring either.
    [Fact]
    public void Zones_OfAFanThatCanBeResizedOrAreLong_AreNoRings()
    {
        var hub = new DeviceInfo("hub", "Lian Li Uni Hub", DeviceType.Fan, 16 + ZonePreviewViewModel.MaxRingLeds + 1,
            [new RgbZone("Channel 1", 16, 0, 200), RgbZone.Fixed("Fans", ZonePreviewViewModel.MaxRingLeds + 1)])
        {
            Source = DeviceSource.Hardware,
        };

        Assert.All(new DevicePreviewViewModel(hub).Zones, zone => Assert.False(zone.IsRing));
    }

    [Fact]
    public void Zones_OfOtherDevices_AreNoRings()
    {
        var strip = TestDevices.Create("strip", "Fan strip", DeviceType.LedStrip, 10);

        Assert.All(new DevicePreviewViewModel(Motherboard).Zones, zone => Assert.False(zone.IsRing));
        Assert.False(Assert.Single(new DevicePreviewViewModel(strip).Zones).IsRing);
    }

    // A key map tells where the LEDs sit, so it wins over the ring.
    [Fact]
    public void Zones_OfAFanWithAKeyMap_AreNoRings()
    {
        var fan = new DeviceInfo("fan", "Fan", DeviceType.Fan, 2, [RgbZone.Fixed("Panel", 2) with { Matrix = new LedMatrix(1, 2, [0, 1]) }])
        {
            Source = DeviceSource.Hardware,
        };

        Assert.False(Assert.Single(new DevicePreviewViewModel(fan).Zones).IsRing);
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
        Assert.Equal([null, null, Strings.EmptyZoneHint], zones.Select(zone => zone.LedCountHelpText));
    }

    [Fact]
    public void Zones_ResizableZoneWithLeds_ShowsNoHint()
    {
        var strip = new DeviceInfo("strip", "LED strip", DeviceType.LedStrip, 30, [new RgbZone("Strip", 30, 1, 60)]) { Source = DeviceSource.Hardware };

        var zone = Assert.Single(new DevicePreviewViewModel(strip).Zones);
        Assert.False(zone.ShowsEmptyZoneHint);
        Assert.Null(zone.LedCountHelpText);
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
    public void FlashZone_AsksToFlashOnlyThatZone()
    {
        var actions = new RecordingPreviewActions();
        var header = new DevicePreviewViewModel(Motherboard, actions).Zones[1];

        header.FlashCommand.Execute(null);

        Assert.Equal([new ZoneFlash("board", 1)], actions.Requests);
        Assert.Equal("Zone RGB header van Motherboard laten knipperen", header.FlashAccessibleName);
    }

    // The empty header has nothing to blink, not even while the user sets a length for it.
    [Fact]
    public void ShowsFlash_OnlyForANamedZoneWithLeds()
    {
        var actions = new RecordingPreviewActions();
        var preview = new DevicePreviewViewModel(Motherboard, actions, new FakeTimeProvider());
        preview.Zones[2].Length!.IncreaseCommand.Execute(null);

        Assert.Equal([true, true, false], preview.Zones.Select(zone => zone.ShowsFlash));
        preview.Zones[2].FlashCommand.Execute(null);
        Assert.Empty(actions.Requests.OfType<ZoneFlash>());
    }

    // A device with one zone has the button of the device; the LEDs that no zone claims are no
    // zone the engine knows.
    [Fact]
    public void ShowsFlash_NotForTheOnlyZoneNorForLedsWithoutZone()
    {
        var single = new DevicePreviewViewModel(new DeviceInfo("ram", "RAM", DeviceType.Ram, 8, [RgbZone.Fixed("DRAM", 8)]) { Source = DeviceSource.Hardware });
        var rest = new DevicePreviewViewModel(new DeviceInfo("board", "Board", DeviceType.Motherboard, 5, [RgbZone.Fixed("A", 2)]) { Source = DeviceSource.Hardware });

        Assert.False(single.Zones[0].ShowsFlash);
        Assert.Equal([true, false], rest.Zones.Select(zone => zone.ShowsFlash));
    }

    [Fact]
    public void FlashZone_WhilePaused_IsGrey()
    {
        var actions = new RecordingPreviewActions { CanFlash = false };
        var header = new DevicePreviewViewModel(Motherboard, actions).Zones[1];

        Assert.False(header.FlashCommand.CanExecute(null));
    }

    [Fact]
    public void ApplyLedCount_EmptyHeader_AsksForTheNewLength()
    {
        var actions = new RecordingPreviewActions();
        var header = new DevicePreviewViewModel(Motherboard, actions).Zones[2];
        var length = header.Length!;

        Assert.True(header.CanResize);
        Assert.Equal("0", length.LedCountText);
        Assert.Equal("Aantal LED's (0–120):", length.Label);
        Assert.Equal("Aantal LED's van Motherboard – ARGB header", length.SliderAccessibleName);
        Assert.Equal("Aantal LED's toepassen voor Motherboard – ARGB header", length.ApplyAccessibleName);
        Assert.False(length.ApplyCommand.CanExecute(null));

        length.LedCountText = "30";
        Assert.True(length.ApplyCommand.CanExecute(null));
        length.ApplyCommand.Execute(null);

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
        var length = new DevicePreviewViewModel(Motherboard).Zones[2].Length!;

        length.LedCountText = text;

        Assert.False(length.ApplyCommand.CanExecute(null));
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
        Assert.Equal("30", zone.Length!.LedCountText);
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

    // Without a zone name to click, "Alles selecteren" selects the one zone, so all the LEDs.
    [Fact]
    public void SelectAll_DeviceWithOneZone_HandsOnAllItsLeds()
    {
        var strip = new DeviceInfo("strip", "LED strip", DeviceType.LedStrip, 30, [RgbZone.Fixed("Strip", 30)]) { Source = DeviceSource.Hardware };
        var actions = new RecordingPreviewActions();
        var zone = Assert.Single(new DevicePreviewViewModel(strip, actions).Zones);

        Assert.True(zone.ShowsSelectAllLink);
        Assert.Equal("Alle LED's van LED strip selecteren", zone.SelectAllAccessibleName);
        zone.SelectZoneCommand.Execute(null);

        Assert.Equal([new LedSelection("strip", "LED strip", 0, 29)], actions.Requests);
    }

    [Fact]
    public void SelectAll_DeviceWithMoreZones_IsNotShown()
    {
        var fan = new DeviceInfo("fan", "Dual-ring fan", DeviceType.Fan, 20, [RgbZone.Fixed("Inner ring", 8), RgbZone.Fixed("Outer ring", 12)]) { Source = DeviceSource.Hardware };

        Assert.All(new DevicePreviewViewModel(fan).Zones, zone => Assert.False(zone.ShowsSelectAllLink));
    }

    // A screen reader reads the items of the preview by their ToString, not the class name.
    [Fact]
    public void ToString_NamesTheDeviceAndTheZone()
    {
        var fan = new DeviceInfo("fan", "Dual-ring fan", DeviceType.Fan, 20, [RgbZone.Fixed("Inner ring", 8), RgbZone.Fixed("Outer ring", 12)]) { Source = DeviceSource.Hardware };
        var preview = new DevicePreviewViewModel(fan);

        Assert.Equal("Dual-ring fan", preview.ToString());
        Assert.Equal(preview.Zones[0].AccessibleName, preview.Zones[0].ToString());
        Assert.Contains("Inner ring", preview.Zones[0].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SelectZone_ZoneWithoutLeds_CannotRunAndShowsPlainText()
    {
        var header = new DevicePreviewViewModel(Motherboard).Zones[2];

        Assert.False(header.SelectZoneCommand.CanExecute(null));
        Assert.False(header.ShowsHeaderLink);
        Assert.True(header.ShowsHeaderText);
    }

    // The preview grows before the engine has the new length; the new LEDs stay dark until then.
    [Fact]
    public void Length_Increased_ShowsTheNewLedsAtOnce()
    {
        var strip = new DeviceInfo("strip", "LED strip", DeviceType.LedStrip, 2, [new RgbZone("Strip", 2, 1, 60)]) { Source = DeviceSource.Hardware };
        var preview = new DevicePreviewViewModel(strip);
        preview.Colors = [Red, Red];
        var zone = Assert.Single(preview.Zones);

        zone.Length!.IncreaseCommand.Execute(null);

        Assert.Equal(3, zone.ShownLedCount);
        Assert.Equal(2, zone.LedCount);
        Assert.Equal([Red, Red, RgbColor.Black], zone.Colors);
        Assert.Equal(["LED 1", "LED 2", "LED 3"], zone.LedLabels);
        Assert.Equal("Strip · 3 LED's", zone.Header);

        preview.Colors = [Green, Green];

        Assert.Equal([Green, Green, RgbColor.Black], zone.Colors);
    }

    [Fact]
    public void Length_Decreased_ShowsFewerLeds()
    {
        var strip = new DeviceInfo("strip", "LED strip", DeviceType.LedStrip, 3, [new RgbZone("Strip", 3, 1, 60)]) { Source = DeviceSource.Hardware };
        var preview = new DevicePreviewViewModel(strip);
        preview.Colors = [Red, Green, Blue];
        var zone = Assert.Single(preview.Zones);

        zone.Length!.DecreaseCommand.Execute(null);

        Assert.Equal([Red, Green], zone.Colors);
        Assert.Equal(2, zone.LedLabels.Count);
    }

    // An LED that only shows because of the new length has no color in the engine to edit.
    [Fact]
    public void ClickLed_OnALedTheZoneDoesNotHaveYet_DoesNothing()
    {
        var actions = new RecordingPreviewActions();
        var strip = new DeviceInfo("strip", "LED strip", DeviceType.LedStrip, 2, [new RgbZone("Strip", 2, 1, 60)]) { Source = DeviceSource.Hardware };
        var zone = Assert.Single(new DevicePreviewViewModel(strip, actions).Zones);
        zone.Length!.IncreaseCommand.Execute(null);

        zone.ClickLedCommand.Execute(new LedHit(2, Extend: false));
        zone.ClickLedCommand.Execute(new LedHit(1, Extend: false));

        Assert.Equal([new LedClick("strip", 1, Extend: false)], actions.Requests);
    }

    // The hint keeps telling the truth: it goes once the user gives the empty header LEDs, and
    // comes back when they go back to none.
    [Fact]
    public void EmptyZoneHint_FollowsTheLengthTheUserSets()
    {
        var header = new DevicePreviewViewModel(Motherboard).Zones[2];
        var changes = new List<string?>();
        header.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        header.Length!.IncreaseCommand.Execute(null);

        Assert.False(header.ShowsEmptyZoneHint);
        Assert.Null(header.LedCountHelpText);
        Assert.Equal("1 LED", header.Details);
        Assert.Contains(nameof(ZonePreviewViewModel.ShowsEmptyZoneHint), changes);

        header.Length.DecreaseCommand.Execute(null);

        Assert.True(header.ShowsEmptyZoneHint);
        Assert.Equal(Strings.EmptyZoneHint, header.LedCountHelpText);
        Assert.Equal(Strings.ZoneWithoutLedsResizable, header.Details);
    }

    [Fact]
    public void EmptyZoneHint_NamesTheWaysToSetTheLength()
    {
        Assert.Contains("+", Strings.EmptyZoneHint);
        Assert.Contains("schuif", Strings.EmptyZoneHint);
    }
}
