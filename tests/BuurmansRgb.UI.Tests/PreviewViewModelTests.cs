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

    private static DeviceInfo Strip(int ledCount) =>
        new("strip", "Strip", DeviceType.LedStrip, ledCount, [new RgbZone("Strip", ledCount, 1, 60)]) { Source = DeviceSource.Hardware };

    private static LightingFrame Frame(long sequence, RgbColor color) =>
        new(sequence, [DeviceFrame.FromColors("fan", [color, color])]);
}
