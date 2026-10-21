using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The keys of the per-LED editor in the preview of a device, as LedPreviewView hands them on.
public sealed class DevicePreviewKeyboardTests
{
    // Six LEDs on the board, a 12 V header of one LED and an empty addressable header.
    private static readonly DeviceInfo Motherboard = new("board", "Motherboard", DeviceType.Motherboard, 7,
        [RgbZone.Fixed("Mainboard", 6), RgbZone.Fixed("RGB header", 1), new RgbZone("ARGB header", 0, 0, 120)]) { Source = DeviceSource.Hardware };

    [Fact]
    public void ActiveLed_StartsAtTheFirstLed()
    {
        var preview = new DevicePreviewViewModel(Motherboard);

        Assert.Equal(0, preview.ActiveLed);
        Assert.Equal([0, -1, -1], preview.Zones.Select(zone => zone.ActiveLed));
    }

    [Fact]
    public void DeviceWithoutLeds_HasNoActiveLedAndTakesNoKeys()
    {
        var actions = new RecordingPreviewActions();
        var preview = new DevicePreviewViewModel(new DeviceInfo("hub", "Hub", DeviceType.LedStrip, 0, [new RgbZone("ARGB", 0, 0, 120)]) { Source = DeviceSource.Hardware }, actions);

        Assert.Equal(LedMatrix.NoLed, preview.ActiveLed);
        Assert.False(preview.PressKey(LedKey.Next));
        Assert.False(preview.PressKey(LedKey.SelectAll));
        Assert.Empty(actions.Requests);
    }

    [Fact]
    public void NextAndPrevious_GoInLedOrderIntoTheNextZone()
    {
        var preview = new DevicePreviewViewModel(Motherboard);

        for (var i = 0; i < 6; i++)
        {
            preview.PressKey(LedKey.Next);
        }

        Assert.Equal(6, preview.ActiveLed);
        Assert.Equal([-1, 0, -1], preview.Zones.Select(zone => zone.ActiveLed));

        preview.PressKey(LedKey.Previous);

        Assert.Equal(5, preview.ShownActiveLed());
    }

    // The keys are still taken at the first and last LED, so the page does not scroll instead.
    [Fact]
    public void NextAndPrevious_StopAtTheFirstAndLastLed()
    {
        var preview = new DevicePreviewViewModel(Motherboard);

        Assert.True(preview.PressKey(LedKey.Previous));
        Assert.Equal(0, preview.ActiveLed);

        preview.PressKey(LedKey.Last);
        Assert.True(preview.PressKey(LedKey.Next));
        Assert.Equal(6, preview.ActiveLed);
    }

    [Fact]
    public void FirstAndLast_GoToTheEndsOfTheDevice()
    {
        var preview = new DevicePreviewViewModel(Motherboard);

        preview.PressKey(LedKey.Last);
        Assert.Equal(6, preview.ShownActiveLed());

        preview.PressKey(LedKey.First);
        Assert.Equal(0, preview.ShownActiveLed());
    }

    // The view tells which LED of the zone is drawn in the row above or below.
    [Fact]
    public void UpAndDown_GoToTheLedTheViewFoundInTheZone()
    {
        var preview = new DevicePreviewViewModel(Motherboard);

        preview.PressKey(LedKey.Down, target: 4);
        Assert.Equal(4, preview.ActiveLed);

        preview.PressKey(LedKey.Up, target: 1);
        Assert.Equal(1, preview.ActiveLed);
    }

    // The zones are drawn one below the other.
    [Fact]
    public void UpAndDown_FromTheTopOrBottomRow_GoToTheZoneBeforeOrAfter()
    {
        var preview = new DevicePreviewViewModel(Motherboard);
        preview.PressKey(LedKey.Next);

        preview.PressKey(LedKey.Down);
        Assert.Equal(6, preview.ActiveLed);

        preview.PressKey(LedKey.Up);
        Assert.Equal(5, preview.ActiveLed);
    }

    // The empty addressable header after the last LED has nothing to go to.
    [Fact]
    public void UpAndDown_WithoutZoneBeforeOrAfter_StayPut()
    {
        var preview = new DevicePreviewViewModel(Motherboard);

        Assert.True(preview.PressKey(LedKey.Up));
        Assert.Equal(0, preview.ActiveLed);

        preview.PressKey(LedKey.Last);
        Assert.True(preview.PressKey(LedKey.Down));
        Assert.Equal(6, preview.ActiveLed);
    }

    // A row the view found past the LEDs of the zone, such as one that only shows for a new
    // length, is no place to go.
    [Fact]
    public void UpAndDown_WithATargetPastTheZone_GoToTheNextZone()
    {
        var preview = new DevicePreviewViewModel(Motherboard);

        preview.PressKey(LedKey.Down, target: 6);

        Assert.Equal(6, preview.ActiveLed);
    }

    [Fact]
    public void Select_ClicksTheActiveLed()
    {
        var actions = new RecordingPreviewActions();
        var preview = new DevicePreviewViewModel(Motherboard, actions);
        preview.PressKey(LedKey.Next);
        preview.PressKey(LedKey.Next);

        preview.PressKey(LedKey.Select);
        preview.PressKey(LedKey.Select, extend: true);

        Assert.Equal([new LedClick("board", 2, Extend: false), new LedClick("board", 2, Extend: true)], actions.Requests);
    }

    // Moving alone chooses nothing; with Shift it is a Shift+click on the next LED, which starts
    // at the LED it came from when nothing of the device is chosen yet.
    [Fact]
    public void ExtendedMove_ClicksTheNextLedWithShiftFromWhereItCame()
    {
        var actions = new RecordingPreviewActions();
        var preview = new DevicePreviewViewModel(Motherboard, actions);
        preview.PressKey(LedKey.Next);

        preview.PressKey(LedKey.Next, extend: true);
        preview.PressKey(LedKey.Last, extend: true);

        Assert.Equal(
            [new LedClick("board", 2, Extend: true, Anchor: 1), new LedClick("board", 6, Extend: true, Anchor: 2)],
            actions.Requests);
    }

    [Fact]
    public void ExtendedMove_AtTheEnd_ClicksNothing()
    {
        var actions = new RecordingPreviewActions();
        var preview = new DevicePreviewViewModel(Motherboard, actions);

        preview.PressKey(LedKey.Previous, extend: true);
        preview.PressKey(LedKey.Up, extend: true);

        Assert.Empty(actions.Requests);
    }

    [Fact]
    public void SelectAll_SelectsEveryLedOfTheDevice()
    {
        var actions = new RecordingPreviewActions();
        var preview = new DevicePreviewViewModel(Motherboard, actions);

        preview.PressKey(LedKey.SelectAll);

        Assert.Equal([new LedSelection("board", "Motherboard", 0, 6)], actions.Requests);
    }

    // Without a selection on this device, Escape is left to the window.
    [Fact]
    public void ClearSelection_OnlyWhileLedsOfTheDeviceAreChosen()
    {
        var actions = new RecordingPreviewActions();
        var preview = new DevicePreviewViewModel(Motherboard, actions);

        Assert.False(preview.PressKey(LedKey.ClearSelection));

        preview.ShowLedSelection(new LedSelection("fan", "Fan", 0, 1));
        Assert.False(preview.PressKey(LedKey.ClearSelection));

        preview.ShowLedSelection(new LedSelection("board", "Motherboard", 2, 3));
        Assert.True(preview.PressKey(LedKey.ClearSelection));
        Assert.Equal(["clear"], actions.Requests);
    }

    [Fact]
    public void ClickLed_MakesItTheActiveLed()
    {
        var preview = new DevicePreviewViewModel(Motherboard, new RecordingPreviewActions());

        preview.ClickLed(6);
        Assert.Equal(6, preview.ShownActiveLed());

        preview.PressKey(LedKey.Previous);
        Assert.Equal(5, preview.ShownActiveLed());
    }

    // So the view that gets the active LED can take the focus from the one that had it.
    [Fact]
    public void ActiveLedToAnotherZone_TheZoneThatLosesItHearsFirst()
    {
        var preview = new DevicePreviewViewModel(Motherboard);
        preview.PressKey(LedKey.Last);
        List<string> heard = [];
        foreach (var zone in preview.Zones)
        {
            zone.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ZonePreviewViewModel.ActiveLed))
                {
                    heard.Add($"{zone.Name} {zone.ActiveLed}");
                }
            };
        }

        preview.PressKey(LedKey.Previous);
        preview.PressKey(LedKey.Next);

        Assert.Equal(["RGB header -1", "Mainboard 5", "Mainboard -1", "RGB header 0"], heard);
    }
}
