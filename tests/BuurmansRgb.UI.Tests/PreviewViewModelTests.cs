using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.UI.Tests;

public sealed class PreviewViewModelTests
{
    private static readonly DeviceInfo Fan = TestDevices.Create("fan", "Fan", DeviceType.Fan, 2);

    [Fact]
    public void ShowDevices_ShowsTheLatestFrameRightAway()
    {
        var preview = new PreviewViewModel();
        preview.ShowFrame(Frame(3, new RgbColor(1, 2, 3)));

        preview.ShowDevices([Fan]);

        Assert.Equal([new RgbColor(1, 2, 3), new RgbColor(1, 2, 3)], Assert.Single(preview.Devices).Colors);
    }

    [Fact]
    public void Reset_AcceptsLowerSequenceFromNewConnection()
    {
        var preview = new PreviewViewModel();
        preview.ShowDevices([Fan]);
        preview.ShowFrame(Frame(9, new RgbColor(1, 2, 3)));

        preview.Reset();
        preview.ShowFrame(Frame(1, new RgbColor(4, 5, 6)));

        Assert.All(preview.Devices[0].Colors, led => Assert.Equal(new RgbColor(4, 5, 6), led));
    }

    [Fact]
    public void ShowDevices_KeepsTheLedSelection()
    {
        var preview = new PreviewViewModel();
        preview.ShowLedSelection(new LedSelection("fan", "Fan", 0, 1));

        preview.ShowDevices([Fan]);

        Assert.Equal((0, 1), preview.Devices[0].ShownSelection());
    }

    // The engine reports the devices again, for example after a resize: the keys go on where they were.
    [Fact]
    public void ShowDevices_KeepsTheActiveLed()
    {
        var preview = new PreviewViewModel();
        preview.ShowDevices([Strip(10), Fan]);
        preview.Devices[0].PressKey(LedKey.Last);

        preview.ShowDevices([Strip(10), Fan]);

        Assert.Equal(9, preview.Devices[0].ShownActiveLed());
        Assert.Equal(0, preview.Devices[1].ShownActiveLed());
    }

    [Fact]
    public void ShowDevices_WithFewerLeds_MovesTheActiveLedToTheLastOne()
    {
        var preview = new PreviewViewModel();
        preview.ShowDevices([Strip(10)]);
        preview.Devices[0].PressKey(LedKey.Last);

        preview.ShowDevices([Strip(4)]);

        Assert.Equal(3, preview.Devices[0].ShownActiveLed());
    }

    // A header before it got two LEDs more: the active LED stays the second one of "ARGB 2", in
    // the zone where FocusKeeper gives the focus back.
    [Fact]
    public void ShowDevices_AfterAnEarlierZoneGrew_KeepsTheActiveLedInItsZone()
    {
        var preview = new PreviewViewModel();
        preview.ShowDevices([Board(2, 3)]);
        preview.Devices[0].ClickLed(3);

        preview.ShowDevices([Board(4, 3)]);

        Assert.Equal(5, preview.Devices[0].ShownActiveLed());
        Assert.Equal([-1, 1], preview.Devices[0].Zones.Select(zone => zone.ActiveLed));
    }

    [Fact]
    public void ShowDevices_WithAShorterZone_MovesTheActiveLedToItsLastOne()
    {
        var preview = new PreviewViewModel();
        preview.ShowDevices([Board(2, 3)]);
        preview.Devices[0].ClickLed(4);

        preview.ShowDevices([Board(2, 1)]);

        Assert.Equal([-1, 0], preview.Devices[0].Zones.Select(zone => zone.ActiveLed));
    }

    // "ARGB 2" has no LEDs now, so the active LED goes to the one with the same number or the
    // last one of the device.
    [Fact]
    public void ShowDevices_WithTheZoneEmpty_KeepsTheLedNumber()
    {
        var preview = new PreviewViewModel();
        preview.ShowDevices([Board(2, 3)]);
        preview.Devices[0].ClickLed(3);

        preview.ShowDevices([Board(5, 0)]);

        Assert.Equal(3, preview.Devices[0].ShownActiveLed());
    }

    [Fact]
    public void PressKeys_RaiseTheEventsOfClicksAndSelections()
    {
        var preview = new PreviewViewModel();
        preview.ShowDevices([Fan]);
        List<object> raised = [];
        preview.LedClicked += (_, click) => raised.Add(click);
        preview.LedsSelected += (_, leds) => raised.Add(leds);
        preview.LedSelectionCleared += (_, _) => raised.Add("clear");

        preview.Devices[0].PressKey(LedKey.Next, extend: true);
        preview.Devices[0].PressKey(LedKey.SelectAll);
        preview.ShowLedSelection(new LedSelection("fan", "Fan", 0, 1));
        preview.Devices[0].PressKey(LedKey.ClearSelection);

        Assert.Equal([new LedClick("fan", 1, Extend: true, Anchor: 0), new LedSelection("fan", "Fan", 0, 1), "clear"], raised);
    }

    [Fact]
    public void ClickLed_RaisesLedClickedWithTheDevice()
    {
        var preview = new PreviewViewModel();
        preview.ShowDevices([Fan]);
        LedClick? clicked = null;
        preview.LedClicked += (_, click) => clicked = click;

        preview.Devices[0].ClickLed(1, extend: true);

        Assert.Equal(new LedClick("fan", 1, Extend: true), clicked);
    }

    // The engine reports the devices again (here after an earlier resize) while the user is still
    // clicking: the preview keeps the new length, and it still goes out once, after the pause.
    [Fact]
    public void ShowDevices_ZoneLengthNotSentYet_StaysAndGoesOutAfterThePause()
    {
        var time = new FakeTimeProvider();
        var preview = new PreviewViewModel(time);
        var resizes = new List<ZoneResize>();
        preview.ZoneResizeRequested += (_, resize) => resizes.Add(resize);
        preview.ShowDevices([Strip(10)]);
        preview.Devices[0].Zones[0].Length!.IncreaseCommand.Execute(null);

        preview.ShowDevices([Strip(10)]);

        var zone = preview.Devices[0].Zones[0];
        Assert.Equal(11, zone.ShownLedCount);
        Assert.Empty(resizes);
        time.Advance(ZoneLengthViewModel.SendDelay);
        Assert.Equal([new ZoneResize("strip", 0, 11)], resizes);
    }

    // For example because the user picked another device: the length goes out right away.
    [Fact]
    public void ShowDevices_ZoneWithALengthNotSentYetGoes_SendsItNow()
    {
        var time = new FakeTimeProvider();
        var preview = new PreviewViewModel(time);
        var resizes = new List<ZoneResize>();
        preview.ZoneResizeRequested += (_, resize) => resizes.Add(resize);
        preview.ShowDevices([Strip(10)]);
        preview.Devices[0].Zones[0].Length!.IncreaseCommand.Execute(null);

        preview.ShowDevices([Fan]);

        Assert.Equal([new ZoneResize("strip", 0, 11)], resizes);
        time.Advance(ZoneLengthViewModel.SendDelay);
        Assert.Single(resizes);
    }

    [Fact]
    public void ShowDevices_ZoneGoesHalfwayADrag_SendsItsLength()
    {
        var preview = new PreviewViewModel(new FakeTimeProvider());
        var resizes = new List<ZoneResize>();
        preview.ZoneResizeRequested += (_, resize) => resizes.Add(resize);
        preview.ShowDevices([Strip(10)]);
        var length = preview.Devices[0].Zones[0].Length!;
        length.BeginDrag();
        length.LedCount = 20;

        preview.ShowDevices([Fan]);

        Assert.Equal([new ZoneResize("strip", 0, 20)], resizes);
    }

    // The window closes before the delay is over, or while the thumb is held.
    [Fact]
    public void Dispose_SendsTheLengthsThatWait()
    {
        var time = new FakeTimeProvider();
        var preview = new PreviewViewModel(time);
        var resizes = new List<ZoneResize>();
        preview.ZoneResizeRequested += (_, resize) => resizes.Add(resize);
        preview.ShowDevices([Strip(10), Strip(10) with { Id = "other" }]);
        preview.Devices[0].Zones[0].Length!.IncreaseCommand.Execute(null);
        var dragged = preview.Devices[1].Zones[0].Length!;
        dragged.BeginDrag();
        dragged.LedCount = 30;

        preview.Dispose();
        time.Advance(ZoneLengthViewModel.SendDelay);

        Assert.Equal([new ZoneResize("strip", 0, 11), new ZoneResize("other", 0, 30)], resizes);
    }

    [Fact]
    public void ShowDevices_LengthAlreadySent_ShowsWhatTheEngineReports()
    {
        var time = new FakeTimeProvider();
        var preview = new PreviewViewModel(time);
        preview.ShowDevices([Strip(10)]);
        preview.Devices[0].Zones[0].Length!.IncreaseCommand.Execute(null);
        time.Advance(ZoneLengthViewModel.SendDelay);

        preview.ShowDevices([Strip(10)]);

        Assert.Equal(10, preview.Devices[0].Zones[0].ShownLedCount);
        Assert.False(preview.Devices[0].Zones[0].Length!.HasUnsentChange);
    }

    private static DeviceInfo Board(int argb1, int argb2) =>
        new("board", "Board", DeviceType.Motherboard, argb1 + argb2, [new RgbZone("ARGB 1", argb1, 0, 60), new RgbZone("ARGB 2", argb2, 0, 60)])
        {
            Source = DeviceSource.Hardware,
        };

    private static DeviceInfo Strip(int ledCount) =>
        new("strip", "Strip", DeviceType.LedStrip, ledCount, [new RgbZone("Strip", ledCount, 1, 60)]) { Source = DeviceSource.Hardware };

    private static LightingFrame Frame(long sequence, RgbColor color) =>
        new(sequence, [DeviceFrame.FromColors("fan", [color, color])]);
}
