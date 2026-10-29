using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.UI.Tests;

// With "Animatie-effecten" of Windows off (a fake setting), the preview stands still: the engine
// sends no frames, and the app fetches one image after each change instead. The devices are not
// touched: the app sends no other calls for it.
public sealed class MainViewModelStillPreviewTests
{
    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly RgbColor Green = new(0, 255, 0);
    private static readonly RgbColor Blue = new(0, 0, 255);

    private readonly FakeEngineConnector _connector = new();
    private readonly FakeAnimationEffects _animations = new();
    private readonly FakeTimeProvider _time = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task AnimationsOff_WantsNoFramesAndShowsOneImage()
    {
        _animations.IsOn = false;
        var viewModel = await ConnectAsync(Frame(1, Red));

        Assert.False(Api.FramesWanted);
        Assert.Equal([Red, Red], FanColors(viewModel));

        // A frame that was on its way still does not move it.
        Api.RaiseFrame(Frame(2, Green));
        Assert.Equal([Red, Red], FanColors(viewModel));
        Assert.Equal([Red, Red, Red], StripColors(viewModel));
    }

    [Fact]
    public async Task LetRun_WantsFramesAndShowsThem()
    {
        _animations.IsOn = false;
        var viewModel = await ConnectAsync(Frame(1, Red));

        viewModel.Motion.ToggleCommand.Execute(null);
        Api.RaiseFrame(Frame(2, Green));

        Assert.True(Api.FramesWanted);
        Assert.Equal([Green, Green], FanColors(viewModel));
    }

    [Fact]
    public async Task StillAgain_WantsNoFramesAndShowsTheLatestImage()
    {
        _animations.IsOn = false;
        var viewModel = await ConnectAsync(Frame(1, Red));
        viewModel.Motion.ToggleCommand.Execute(null);
        Api.Frame = Frame(5, Blue);

        viewModel.Motion.ToggleCommand.Execute(null);
        Api.RaiseFrame(Frame(6, Green));

        Assert.False(Api.FramesWanted);
        Assert.Equal([Blue, Blue], FanColors(viewModel));
    }

    // Windows turns animations off and on while the app is open.
    [Fact]
    public async Task AnimationsTurnedOffAndOn_FollowsLive()
    {
        var viewModel = await ConnectAsync(Frame(1, Red));
        Api.RaiseFrame(Frame(2, Green));
        Assert.Equal([Green, Green], FanColors(viewModel));

        Api.Frame = Frame(3, Blue);
        _animations.Turn(on: false);
        Api.RaiseFrame(Frame(4, Red));
        Assert.False(Api.FramesWanted);
        Assert.Equal([Blue, Blue], FanColors(viewModel));

        _animations.Turn(on: true);
        Api.RaiseFrame(Frame(5, Green));
        Assert.True(Api.FramesWanted);
        Assert.Equal([Green, Green], FanColors(viewModel));
    }

    // A change from here, such as the brightness, shows in a new image once the engine has it.
    [Fact]
    public async Task ChangeFromHere_ShowsANewImage()
    {
        _animations.IsOn = false;
        var viewModel = await ConnectAsync(Frame(1, Red));
        Api.Frame = Frame(2, Blue);

        viewModel.Brightness = 40;

        Assert.Contains(Api.Calls, call => call.StartsWith("brightness", StringComparison.Ordinal));
        Assert.Equal([Blue, Blue], FanColors(viewModel));
    }

    // A change from elsewhere, such as a scene or a pause, does too.
    [Fact]
    public async Task ChangeFromElsewhere_ShowsANewImage()
    {
        _animations.IsOn = false;
        var viewModel = await ConnectAsync(Frame(1, Red));

        Api.Frame = Frame(2, Blue);
        Api.RaiseLightingChanged();
        Assert.Equal([Blue, Blue], FanColors(viewModel));

        Api.Frame = Frame(3, Green);
        Api.RaisePause(PauseReasons.Manual);
        Assert.Equal([Green, Green], FanColors(viewModel));
    }

    // Each step of a count gives an image: one LED more each time, nothing that goes on and off.
    [Fact]
    public async Task CountStep_ShowsANewImage()
    {
        _animations.IsOn = false;
        await ConnectAsync(Frame(1, Red));
        var before = Api.FrameCalls;

        Api.RaiseZoneCount(new ZoneCountInfo("strip", 0, 2, 3, 1, true) { Sequence = 1 });

        Assert.Equal(before + 1, Api.FrameCalls);
    }

    // Away from a page with the preview, or minimized, no image is fetched; back, one is.
    [Fact]
    public async Task PageWithoutPreview_FetchesNoImageUntilBack()
    {
        _animations.IsOn = false;
        var viewModel = await ConnectAsync(Frame(1, Red));
        viewModel.Navigation.GoTo(AppPage.Scenes);
        var away = Api.FrameCalls;

        Api.RaiseLightingChanged();
        viewModel.Navigation.GoTo(AppPage.Color);
        viewModel.IsWindowMinimized = true;
        var minimized = Api.FrameCalls;
        Api.RaiseLightingChanged();
        Assert.Equal(away + 1, minimized);
        Assert.Equal(minimized, Api.FrameCalls);

        Api.Frame = Frame(2, Blue);
        viewModel.IsWindowMinimized = false;
        Assert.Equal([Blue, Blue], FanColors(viewModel));
    }

    // Knipperen while it stands still: a mark on the card instead of white on and off, and no
    // image until the flash is over, so the white of the flash never shows.
    [Fact]
    public async Task Flash_MarksTheCardAndShowsNoWhite()
    {
        _animations.IsOn = false;
        var viewModel = await ConnectAsync(Frame(1, Red));
        var fan = viewModel.Preview.Devices.Single(device => device.DeviceId == "fan");

        Api.Frame = Frame(2, RgbColor.White);
        fan.FlashCommand.Execute(null);
        Api.RaiseLightingChanged();

        Assert.Contains("flash fan", Api.Calls);
        Assert.Equal("Knippert nu op het apparaat", fan.FlashMark);
        Assert.Equal([Red, Red], FanColors(viewModel));

        Api.Frame = Frame(3, Blue);
        _time.Advance(PreviewViewModel.FlashTime);
        Assert.Null(fan.FlashMark);
        Assert.Equal([Red, Red], FanColors(viewModel));

        _time.Advance(MainViewModel.StillAfterFlash);
        Assert.Equal([Blue, Blue], FanColors(viewModel));
    }

    [Fact]
    public async Task ZoneFlash_NamesTheZoneInTheMark()
    {
        _animations.IsOn = false;
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api => api.Devices =
            [new DeviceInfo("board", "Board", DeviceType.Motherboard, 5, [RgbZone.Fixed("Mainboard", 2), RgbZone.Fixed("ARGB 1", 3)]) { Source = DeviceSource.Hardware }]),
            _time, animationEffects: _animations);
        await viewModel.ConnectCommand.ExecuteAsync(null);
        var board = viewModel.Preview.Devices.Single();

        board.Zones[1].FlashCommand.Execute(null);

        Assert.Equal("ARGB 1 knippert nu op het apparaat", board.FlashMark);
        _time.Advance(PreviewViewModel.FlashTime);
        Assert.Null(board.FlashMark);
    }

    // With animations on, a flash shows as the engine renders it: no mark, frames as always.
    [Fact]
    public async Task Flash_WithAnimationsOn_HasNoMark()
    {
        var viewModel = await ConnectAsync(Frame(1, Red));
        var fan = viewModel.Preview.Devices.Single(device => device.DeviceId == "fan");

        fan.FlashCommand.Execute(null);
        Api.RaiseFrame(Frame(2, RgbColor.White));

        Assert.Null(fan.FlashMark);
        Assert.Equal([RgbColor.White, RgbColor.White], FanColors(viewModel));
    }

    private async Task<MainViewModel> ConnectAsync(LightingFrame frame)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api => api.Frame = frame), _time, animationEffects: _animations);
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }

    private static IReadOnlyList<RgbColor> FanColors(MainViewModel viewModel) =>
        viewModel.Preview.Devices.Single(device => device.DeviceId == "fan").Colors;

    private static IReadOnlyList<RgbColor> StripColors(MainViewModel viewModel) =>
        viewModel.Preview.Devices.Single(device => device.DeviceId == "strip").Colors;

    private static LightingFrame Frame(long sequence, RgbColor color) =>
        new(sequence, [DeviceFrame.FromColors("strip", [color, color, color]), DeviceFrame.FromColors("fan", [color, color])]);
}
