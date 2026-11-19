using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.UI.Tests;

// The cards of "Per apparaat" on Kleur that open and close (DESIGN.md § 9.3 and 9.7).
public sealed class PreviewCardsViewModelTests
{
    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly RgbColor Blue = new(0, 0, 255);
    private static readonly DeviceInfo Fan = TestDevices.Create("fan", "Fan", DeviceType.Fan, 2);

    // "Hele huis" starts with every card closed, with the chips and the links (besluit 7).
    [Fact]
    public void WholeHouseWithTwoDevices_ShowsClosedCards()
    {
        var preview = new PreviewViewModel();

        preview.ShowDevices([Board(20, 10), Fan], wholeHouse: true);

        Assert.True(preview.UsesCards);
        Assert.All(preview.Devices, device =>
        {
            Assert.True(device.IsFoldable);
            Assert.False(device.IsExpanded);
        });
    }

    // One chosen device, or a house with one device: one card, always open (§ 9.3).
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OneDevice_ShowsACardThatIsAlwaysOpen(bool wholeHouse)
    {
        var preview = new PreviewViewModel();

        preview.ShowDevices([Fan], wholeHouse);

        Assert.False(preview.UsesCards);
        var device = Assert.Single(preview.Devices);
        Assert.False(device.IsFoldable);
        Assert.True(device.IsExpanded);
    }

    // Open or closed stays while the app runs: also after one chosen device and back to "Hele huis",
    // and when the engine reports the devices again (besluit 3). A new device comes closed.
    [Fact]
    public void OpenCard_StaysOpenAfterOneDeviceAndBack()
    {
        var preview = new PreviewViewModel();
        preview.ShowDevices([Board(20, 10), Fan], wholeHouse: true);
        preview.Devices[1].IsExpanded = true;

        preview.ShowDevices([Fan]);
        preview.ShowDevices([Board(20, 10), Fan, Strip(8)], wholeHouse: true);

        Assert.Equal([false, true, false], preview.Devices.Select(device => device.IsExpanded));
    }

    [Fact]
    public void ExpandAllAndCollapseAll_OpenAndCloseEveryCard()
    {
        var preview = new PreviewViewModel();
        preview.ShowDevices([Board(20, 10), Fan, Strip(8)], wholeHouse: true);

        preview.ExpandAllCommand.Execute(null);
        Assert.All(preview.Devices, device => Assert.True(device.IsExpanded));

        preview.CollapseAllCommand.Execute(null);
        Assert.All(preview.Devices, device => Assert.False(device.IsExpanded));
    }

    // The chips say where they are in the row for a screen reader: "2 van 3".
    [Fact]
    public void Devices_KnowTheirPlaceInTheRow()
    {
        var preview = new PreviewViewModel();

        preview.ShowDevices([Board(20, 10), Fan, Strip(8)], wholeHouse: true);

        Assert.Equal([(1, 3), (2, 3), (3, 3)], preview.Devices.Select(device => (device.Position, device.SetSize)));
    }

    // Closing a card stops the count this app runs in it, so the zone gets its own length back
    // (besluit 6).
    [Fact]
    public void Collapse_StopsTheOwnCountInTheCard()
    {
        var preview = new PreviewViewModel();
        List<ZoneCountStop> stops = [];
        preview.ZoneCountStopRequested += (_, stop) => stops.Add(stop);
        preview.ShowDevices([Board(20, 10), Fan], wholeHouse: true);
        var board = preview.Devices[0];
        board.IsExpanded = true;
        board.Zones[1].CountLedsCommand.Execute(null);
        preview.ShowZoneCount(new ZoneCountInfo("board", 1, 4, 60, 1, true));

        preview.Devices[1].IsExpanded = true;
        preview.Devices[1].IsExpanded = false;
        Assert.Empty(stops);
        board.IsExpanded = false;

        Assert.Equal([new ZoneCountStop("board", 1)], stops);
        Assert.Null(preview.Count);
    }

    // A count another settings app runs goes on, as when the user leaves Kleur.
    [Fact]
    public void Collapse_LeavesACountOfAnotherApp()
    {
        var preview = new PreviewViewModel();
        List<ZoneCountStop> stops = [];
        preview.ZoneCountStopRequested += (_, stop) => stops.Add(stop);
        preview.ShowDevices([Board(20, 10), Fan], wholeHouse: true);
        preview.Devices[0].IsExpanded = true;
        preview.ShowZoneCount(new ZoneCountInfo("board", 1, 4, 60, 1, true));

        preview.Devices[0].IsExpanded = false;

        Assert.Empty(stops);
    }

    // Chosen LEDs in a card that closes are let go, so a color does not go to LEDs out of sight;
    // those of another card stay.
    [Fact]
    public void Collapse_LetsGoOfTheChosenLedsInTheCard()
    {
        var preview = new PreviewViewModel();
        var cleared = 0;
        preview.LedSelectionCleared += (_, _) => cleared++;
        preview.ShowDevices([Board(20, 10), Fan], wholeHouse: true);
        preview.ExpandAll();
        preview.ShowLedSelection(new LedSelection("fan", "Fan", 0, 1));

        preview.Devices[0].IsExpanded = false;
        Assert.Equal(0, cleared);
        preview.Devices[1].IsExpanded = false;

        Assert.Equal(1, cleared);
    }

    // A house with one device gets a second one, or OpenRGB reports one device for a moment: the
    // card that was always open comes closed (besluit 7), so what ran in it stops, as when the user
    // closes it (besluit 6).
    [Fact]
    public void SecondDevice_ClosesTheAlwaysOpenCardAndStopsWhatRanInIt()
    {
        var preview = new PreviewViewModel();
        List<ZoneCountStop> stops = [];
        var cleared = 0;
        preview.ZoneCountStopRequested += (_, stop) => stops.Add(stop);
        preview.LedSelectionCleared += (_, _) => cleared++;
        preview.ShowDevices([Board(20, 10)], wholeHouse: true);
        preview.Devices[0].Zones[1].CountLedsCommand.Execute(null);
        preview.ShowZoneCount(new ZoneCountInfo("board", 1, 4, 60, 1, true));
        preview.ShowLedSelection(new LedSelection("board", "Board", 0, 1));

        preview.ShowDevices([Board(20, 10), Fan], wholeHouse: true);

        Assert.False(preview.Devices[0].IsExpanded);
        Assert.Equal([new ZoneCountStop("board", 1)], stops);
        Assert.Null(preview.Count);
        Assert.Equal(1, cleared);
    }

    // A length that waits in the card that was always open goes out at once when that card comes
    // closed, as when the user closes it (§ 9.4): after a second device in the house, or back from
    // one chosen device to "Hele huis" within the delay.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AlwaysOpenCardThatComesClosed_SendsAWaitingLength(bool wholeHouse)
    {
        var preview = new PreviewViewModel(new FakeTimeProvider());
        List<ZoneResize> resizes = [];
        preview.ZoneResizeRequested += (_, resize) => resizes.Add(resize);
        preview.ShowDevices([Board(20, 10)], wholeHouse);
        preview.Devices[0].Zones[0].Length!.IncreaseCommand.Execute(null);
        Assert.Empty(resizes);

        preview.ShowDevices([Board(20, 10), Fan], wholeHouse: true);

        Assert.False(preview.Devices[0].IsExpanded);
        Assert.Equal([new ZoneResize("board", 0, 21)], resizes);
    }

    // Back from one chosen device to "Hele huis": its card comes closed unless it was open there,
    // and only a closed one stops what ran in it.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BackToWholeHouse_StopsTheCountOnlyWhenTheCardComesClosed(bool openedBefore)
    {
        var preview = new PreviewViewModel();
        List<ZoneCountStop> stops = [];
        preview.ZoneCountStopRequested += (_, stop) => stops.Add(stop);
        preview.ShowDevices([Board(20, 10), Fan], wholeHouse: true);
        preview.Devices[0].IsExpanded = openedBefore;
        preview.ShowDevices([Board(20, 10)]);
        preview.Devices[0].Zones[1].CountLedsCommand.Execute(null);
        preview.ShowZoneCount(new ZoneCountInfo("board", 1, 4, 60, 1, true));

        preview.ShowDevices([Board(20, 10), Fan], wholeHouse: true);

        Assert.Equal(openedBefore, preview.Devices[0].IsExpanded);
        Assert.Equal(openedBefore ? 0 : 1, stops.Count);
        Assert.Equal(openedBefore, preview.Count is not null);
    }

    // A length that still waits for its delay goes to the engine when the card closes, as if
    // Toepassen was chosen.
    [Fact]
    public void Collapse_SendsAWaitingLength()
    {
        var preview = new PreviewViewModel(new FakeTimeProvider());
        List<ZoneResize> resizes = [];
        preview.ZoneResizeRequested += (_, resize) => resizes.Add(resize);
        preview.ShowDevices([Board(20, 10), Fan], wholeHouse: true);
        preview.Devices[0].IsExpanded = true;
        preview.Devices[0].Zones[0].Length!.IncreaseCommand.Execute(null);
        Assert.Empty(resizes);

        preview.Devices[0].IsExpanded = false;

        Assert.Equal([new ZoneResize("board", 0, 21)], resizes);
    }

    // A closed card draws no LEDs, so its zones get no colors; opened, they show the colors of now.
    // Its small strip follows the colors also while closed.
    [Fact]
    public void ClosedCard_GivesItsZonesNoColorsUntilItOpens()
    {
        var preview = new PreviewViewModel();
        preview.ShowDevices([Strip(8), Fan], wholeHouse: true);
        var strip = preview.Devices[0];

        preview.ShowFrame(new LightingFrame(1, [DeviceFrame.FromColors("strip", Enumerable.Repeat(Red, 8).ToArray())]));
        Assert.All(strip.Zones[0].Colors, color => Assert.Equal(RgbColor.Black, color));
        Assert.All(strip.Strip, color => Assert.Equal(Red, color));

        strip.IsExpanded = true;

        Assert.All(strip.Zones[0].Colors, color => Assert.Equal(Red, color));
    }

    // Twelve bulbs spread over the device, each the LED in the middle of its twelfth, as a new list
    // per frame; the chip shows the first, and black, as of a device that is off, as led-off.
    [Fact]
    public void Strip_ShowsTwelveLedsSpreadOverTheDevice()
    {
        var device = new DevicePreviewViewModel(Strip(24));
        RgbColor[] colors = [.. Enumerable.Range(0, 24).Select(led => led % 2 == 0 ? RgbColor.Black : Blue)];
        var before = device.Strip;

        device.Colors = colors;

        Assert.NotSame(before, device.Strip);
        Assert.Equal(Enumerable.Repeat(Blue, DevicePreviewViewModel.StripLength), device.Strip);
        Assert.Equal((Blue, true), (device.ChipLed.Color, device.ChipLed.IsLit));
        device.Colors = new RgbColor[24];
        Assert.False(device.ChipLed.IsLit);
    }

    // The line under the name on a card: the LEDs and, with more zones, the zones, without the type.
    [Fact]
    public void LedLine_SaysTheLedsAndTheZonesWithoutTheType()
    {
        Assert.Equal("30 LED's in 2 zones", new DevicePreviewViewModel(Board(20, 10)).LedLine);
        Assert.Equal("2 LED's", new DevicePreviewViewModel(Fan).LedLine);
        Assert.Equal("1 LED", new DevicePreviewViewModel(Strip(1)).LedLine);
    }

    private static DeviceInfo Board(int argb1, int argb2) =>
        new("board", "Board", DeviceType.Motherboard, argb1 + argb2, [new RgbZone("ARGB 1", argb1, 0, 60), new RgbZone("ARGB 2", argb2, 0, 60)])
        {
            Source = DeviceSource.Hardware,
        };

    private static DeviceInfo Strip(int ledCount) =>
        new("strip", "Strip", DeviceType.LedStrip, ledCount, [new RgbZone("Strip", ledCount, 1, 60)]) { Source = DeviceSource.Hardware };
}
