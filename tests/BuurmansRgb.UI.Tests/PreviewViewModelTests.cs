using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class PreviewViewModelTests
{
    private static readonly DeviceInfo Fan = new("fan", "Fan", DeviceType.Fan, 2);

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

        Assert.Equal((0, 1), (preview.Devices[0].SelectionFirst, preview.Devices[0].SelectionLast));
    }

    [Fact]
    public void ClickLed_RaisesLedClickedWithTheDevice()
    {
        var preview = new PreviewViewModel();
        preview.ShowDevices([Fan]);
        LedClick? clicked = null;
        preview.LedClicked += (_, click) => clicked = click;

        preview.Devices[0].ClickLedCommand.Execute(new LedHit(1, Extend: true));

        Assert.Equal(new LedClick("fan", 1, Extend: true), clicked);
    }

    private static LightingFrame Frame(long sequence, RgbColor color) =>
        new(sequence, [DeviceFrame.FromColors("fan", [color, color])]);
}
