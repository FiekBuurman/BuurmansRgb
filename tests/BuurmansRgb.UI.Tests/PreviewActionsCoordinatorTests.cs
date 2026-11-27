using BuurmansRgb.Contracts;
using BuurmansRgb.Contracts.Testing;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.UI.Tests;

// What the user asks for in the preview, without MainViewModel: a session on a fake engine, and a
// preview made with the coordinator, the stills and the LED edit it hands the requests on to. The
// strip of three LEDs can be resized, the fan of two not.
public sealed class PreviewActionsCoordinatorTests : IDisposable
{
    private readonly FakeEngineConnection _connection = new();
    private readonly FakeTimeProvider _time = new();
    private readonly EngineSession _session;
    private readonly PreviewViewModel _preview;
    private readonly PreviewMotionViewModel _motion = new(new FakeAnimationEffects());
    private readonly StillPreviewCoordinator _stills;
    private readonly LedEditCoordinator _ledEdit;
    private readonly PreviewActionsCoordinator _actions;
    private int _selectionsCleared;

    public PreviewActionsCoordinatorTests()
    {
        _session = new EngineSession(new UiThread(), _ => { }, _ => { });
        _actions = new PreviewActionsCoordinator(_session, () => _preview!, () => _stills!, () => _ledEdit!);
        _preview = new PreviewViewModel(_time, _actions);
        _stills = new StillPreviewCoordinator(_session, _time, _preview, new LampsViewModel(), _motion, new EffectOptionsViewModel(), () => true, () => null, () => "fan");
        _ledEdit = new LedEditCoordinator(_session, new LedEditorViewModel(), _preview, new LampsViewModel(), () => Api.Devices, () =>
        {
            _selectionsCleared++;
            return Task.CompletedTask;
        });
        // A strip whose length can be set, so it can count.
        Api.Devices = [Api.Devices[0] with { Zones = [new RgbZone("LED strip", 3, 0, 20)] }, Api.Devices[1]];
        _session.Attach(_connection);
        _preview.ShowDevices(Api.Devices, wholeHouse: true);
    }

    private FakeEngineApi Api => _connection.FakeApi;

    public void Dispose()
    {
        _stills.Dispose();
        _motion.Dispose();
        _preview.Dispose();
    }

    [Fact]
    public void ResizeZone_GoesToTheEngine()
    {
        _actions.ResizeZone(new ZoneResize("strip", 0, 5));

        Assert.Equal(["resize strip 0 5"], Api.Calls);
    }

    [Fact]
    public void SetZonesAlike_GoesToTheEngine()
    {
        _actions.SetZonesAlike(new ZonesAlikeChoice("fan", Alike: true));

        Assert.Equal(["zones-alike fan on"], Api.Calls);
    }

    [Fact]
    public void ZoneCount_StartChangeAndStop_GoToTheEngine()
    {
        _actions.StartZoneCount(new ZoneCountStart("strip", 0, 4));
        _actions.ChangeZoneCount(new ZoneCountUpdate("strip", 0, new ZoneCountChange(Running: false)));
        _actions.StopZoneCount(new ZoneCountStop("strip", 0));

        Assert.Equal(["count strip 0 4", "count strip 0 pause", "count strip 0 stop"], Api.Calls);
    }

    // A count goes at once; the flash of a lamp that waits from the list would blink over it.
    [Fact]
    public void StartZoneCount_CancelsAFlashThatWaits()
    {
        _stills.FlashChosen("strip");
        _stills.FlashChosen("fan");

        _actions.StartZoneCount(new ZoneCountStart("strip", 0, 4));
        _time.Advance(StillPreviewCoordinator.FlashRest);

        Assert.Equal(["flash strip", "count strip 0 4"], Api.Calls);
    }

    [Fact]
    public void FlashDeviceAndZone_GoThroughTheStills()
    {
        _actions.FlashDevice("fan");
        _actions.FlashZone(new ZoneFlash("strip", 0));

        Assert.Equal(["flash fan", "flash strip zone 0"], Api.Calls);
    }

    // The count of this app stops when its zone goes from view.
    [Fact]
    public void StartZoneCount_ThenTheZoneGoesFromView_StopsTheCount()
    {
        ((IPreviewRequests)_preview).StartZoneCount(new ZoneCountStart("strip", 0, 4));
        _preview.ShowZoneCount(new ZoneCountInfo("strip", 0, 1, 3, 4, true) { Sequence = 3 });

        _preview.ShowDevices([.. Api.Devices.Where(device => device.Id == "fan")], wholeHouse: true);

        Assert.Equal(["count strip 0 4", "count strip 0 stop"], Api.Calls);
    }

    // The engine refused the start: the count that comes after it is not this app's, so it stays.
    [Fact]
    public async Task StartZoneCount_Failed_ForgetsTheStart()
    {
        Api.FailOnce = ("count strip 0 4", new InvalidOperationException("refused"));
        ((IPreviewRequests)_preview).StartZoneCount(new ZoneCountStart("strip", 0, 4));
        // The call queue goes in order: once this call went, the failed start was handled.
        _actions.SetZonesAlike(new ZonesAlikeChoice("fan", Alike: true));
        await Eventually.TrueAsync(() => Api.Calls.Count == 1);

        _preview.ShowZoneCount(new ZoneCountInfo("strip", 0, 1, 3, 4, true) { Sequence = 3 });
        _preview.ShowDevices([.. Api.Devices.Where(device => device.Id == "fan")], wholeHouse: true);

        Assert.Equal(["zones-alike fan on"], Api.Calls);
    }

    [Fact]
    public async Task ClearLedSelection_GoesToTheLedEdit()
    {
        _actions.ClickLed(new LedClick("strip", 0, Extend: false));
        await Eventually.TrueAsync(() => _preview.Devices.Single(device => device.DeviceId == "strip").ShownSelection() == (0, 0));

        _actions.ClearLedSelection();

        Assert.Equal(1, _selectionsCleared);
        Assert.Equal((-1, -1), _preview.Devices.Single(device => device.DeviceId == "strip").ShownSelection());
    }
}
