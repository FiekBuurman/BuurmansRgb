using BuurmansRgb.Contracts;
using BuurmansRgb.Contracts.Testing;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.UI.Tests;

// The frames, the still images and the flash of the preview, without MainViewModel: a session on a
// fake engine, and the preview, the lamps and the motion that the coordinator shows them in.
public sealed class StillPreviewCoordinatorTests : IDisposable
{
    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly RgbColor Blue = new(0, 0, 255);

    private readonly FakeEngineConnection _connection = new();
    private readonly FakeAnimationEffects _animations = new();
    private readonly FakeTimeProvider _time = new();
    private readonly PreviewViewModel _preview;
    private readonly PreviewMotionViewModel _motion;
    private readonly StillPreviewCoordinator _stills;
    private bool _showsPreview = true;
    private string? _frameDevice;
    private string? _selectedDeviceId;

    public StillPreviewCoordinatorTests()
    {
        StillPreviewCoordinator? stills = null;
        var session = new EngineSession(new UiThread(), _ => { }, key => stills?.OnCallSucceeded(key));
        _preview = new PreviewViewModel(_time);
        _motion = new PreviewMotionViewModel(_animations);
        stills = new StillPreviewCoordinator(session, _time, _preview, new LampsViewModel(), _motion, new EffectOptionsViewModel(), () => _showsPreview, () => _frameDevice, () => _selectedDeviceId);
        _stills = stills;
        session.Attach(_connection);
        _preview.ShowDevices(Api.Devices, wholeHouse: true);
    }

    private FakeEngineApi Api => _connection.FakeApi;

    public void Dispose()
    {
        _stills.Dispose();
        _motion.Dispose();
        _preview.Dispose();
    }

    // The engine starts with frames of every device; a connection says only what differs.
    [Fact]
    public void OnConnected_WhileStill_AsksNoFramesOnce()
    {
        _animations.IsOn = false;

        _stills.OnConnected();
        _stills.SendFramesWanted();
        _stills.SendFrameDevices();

        Assert.Equal([false], Api.FramesWantedCalls);
        Assert.Empty(Api.FrameDevicesCalls);
    }

    [Fact]
    public void SendFrameDevices_AsksTheOneDeviceOnlyWhenItChanges()
    {
        _stills.OnConnected();
        _frameDevice = "fan";

        _stills.SendFrameDevices();
        _stills.SendFrameDevices();

        Assert.Equal(["fan"], Assert.Single(Api.FrameDevicesCalls)!);
    }

    [Fact]
    public void RequestStill_WhileStill_ShowsOneImage()
    {
        _animations.IsOn = false;
        Api.Frame = Frame(1, Red);

        _stills.RequestStill();

        Assert.Equal(1, Api.FrameCalls);
        Assert.Equal([Red, Red], FanColors());
    }

    [Fact]
    public void RequestStill_WhileMovingOrWithoutPreview_FetchesNothing()
    {
        _stills.RequestStill();
        _animations.IsOn = false;
        _showsPreview = false;
        _stills.RequestStill();

        Assert.Equal(0, Api.FrameCalls);
    }

    // A frame that was on its way when the preview came to stand still is dropped.
    [Fact]
    public void OnFrameRendered_ShowsOnlyWhileMoving()
    {
        _stills.OnFrameRendered(Frame(1, Red));
        _animations.Turn(on: false);
        _stills.OnFrameRendered(Frame(2, Blue));

        Assert.Equal([Red, Red], FanColors());
    }

    [Fact]
    public void FlashChosen_PicksInARow_FlashOnlyTheLastOneAfterTheRest()
    {
        _selectedDeviceId = "fan";
        _stills.FlashChosen("strip");
        _stills.FlashChosen("fan");

        Assert.Equal(["flash strip"], Api.Calls);

        _time.Advance(StillPreviewCoordinator.FlashRest);
        Assert.Equal(["flash strip", "flash fan"], Api.Calls);
    }

    // The pick that waits is not the selected device any more, as the user went on to "Hele huis".
    [Fact]
    public void FlashChosen_PickThatWaits_GoesWhenTheSelectionMovedOn()
    {
        _stills.FlashChosen("strip");
        _stills.FlashChosen("fan");
        _selectedDeviceId = null;

        _time.Advance(StillPreviewCoordinator.FlashRest);

        Assert.Equal(["flash strip"], Api.Calls);
    }

    // A flash that went through marks the card of a preview that stands still and holds its next
    // image until the flash is over, so it never shows the white of it.
    [Fact]
    public void FlashDevice_WhileStill_MarksAndHoldsTheImage()
    {
        _animations.IsOn = false;
        Api.Frame = Frame(1, RgbColor.White);

        _stills.FlashDevice("fan");
        _stills.RequestStill();

        Assert.NotNull(Fan().FlashMark);
        Assert.Equal(0, Api.FrameCalls);

        Api.Frame = Frame(2, Blue);
        _time.Advance(PreviewViewModel.FlashTime + StillPreviewCoordinator.StillAfterFlash);
        Assert.Equal([Blue, Blue], FanColors());
    }

    // After a change of the lighting, one more image once the scene fade is surely over.
    [Fact]
    public void RequestStillAfterFade_FetchesAnImageAfterTheFade()
    {
        _animations.IsOn = false;

        _stills.RequestStillAfterFade();
        _time.Advance(SceneFade.Default + StillPreviewCoordinator.StillAfterFlash);

        Assert.Equal(1, Api.FrameCalls);
    }

    private DevicePreviewViewModel Fan() => _preview.Devices.Single(device => device.DeviceId == "fan");

    private IReadOnlyList<RgbColor> FanColors() => Fan().Colors;

    private static LightingFrame Frame(long sequence, RgbColor color) =>
        new(sequence, [DeviceFrame.FromColors("strip", [color, color, color]), DeviceFrame.FromColors("fan", [color, color])]);
}
