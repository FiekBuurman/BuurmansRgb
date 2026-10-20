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

    private static LightingFrame Frame(long sequence, RgbColor color) =>
        new(sequence, [DeviceFrame.FromColors("fan", [color, color])]);
}
