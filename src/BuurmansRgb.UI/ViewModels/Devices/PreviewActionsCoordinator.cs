using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.ViewModels;

// What the user asks for in the preview, after PreviewViewModel followed it for the count of LEDs
// (ZoneCountTracker): a new zone length, "Zones gelijk" and the count go to the engine of the
// session, a click on LEDs to the LED edit (LedEditCoordinator) and a flash to the stills
// (StillPreviewCoordinator). A coordinator: PreviewViewModel is made with it, so it gets those it
// works with as functions, which give them once MainViewModel made them. Used on the UI thread.
internal sealed class PreviewActionsCoordinator(
    EngineSession session,
    Func<PreviewViewModel> preview,
    Func<StillPreviewCoordinator> stills,
    Func<LedEditCoordinator> ledEdit) : IPreviewRequests
{
    // The kinds of call in the call queue; with the device, the zone and for a count the kind of
    // change, they make the key that lets a newer call replace one that still waits.
    private const string ResizeCall = "resize";
    private const string CountCall = "count";
    private const string ZonesAlikeCall = "zones-alike";

    public void ClickLed(LedClick click) => _ = ledEdit().ClickLedAsync(click);

    public void SelectLeds(LedSelection selection) => _ = ledEdit().SelectLedsAsync(selection);

    public void ClearLedSelection() => _ = ledEdit().ClearSelectionAsync();

    // The engine reports the device with its new length through DevicesChanged, which rebuilds
    // the preview; an LED edit of that device ends then, because its LEDs moved.
    public void ResizeZone(ZoneResize resize) =>
        session.Post((ResizeCall, resize.DeviceId, resize.ZoneIndex), api => api.ResizeZoneAsync(resize.DeviceId, resize.ZoneIndex, resize.LedCount));

    public void FlashDevice(string deviceId) => stills().FlashDevice(deviceId);

    public void FlashZone(ZoneFlash flash) => stills().FlashZone(flash);

    // The engine reports the device with its new choice through DevicesChanged, which rebuilds the
    // preview; the frames that follow show the zones alike, or each with its own part again.
    public void SetZonesAlike(ZonesAlikeChoice choice) =>
        session.Post((ZonesAlikeCall, choice.DeviceId), api => api.SetZonesAlikeAsync(choice.DeviceId, choice.Alike));

    public void StartZoneCount(ZoneCountStart start) =>
        SendZoneCount(start.DeviceId, start.ZoneIndex, "start", api => StartZoneCountAsync(api, start));

    public void ChangeZoneCount(ZoneCountChangeRequest request) =>
        SendZoneCount(request.DeviceId, request.ZoneIndex, request.Change, api => api.SetZoneCountAsync(request.DeviceId, request.ZoneIndex, request.Change));

    public void StopZoneCount(ZoneCountStop stop) =>
        SendZoneCount(stop.DeviceId, stop.ZoneIndex, "stop", api => api.StopZoneCountAsync(stop.DeviceId, stop.ZoneIndex));

    // A count of LEDs: the engine reports how it goes through ZoneCountChanged, and the device at
    // its longest, or with its length back, through DevicesChanged. Each kind of call has its own
    // key, so a change does not replace a start or another change that still waits.
    private void SendZoneCount(string deviceId, int zoneIndex, object kind, Func<IEngineApi, Task> send)
    {
        stills().CancelPendingFlash();
        session.Post((CountCall, deviceId, zoneIndex, kind), send);
    }

    // A start that fails is not a count of this app; the failure itself goes on to OnCallFailed.
    private async Task StartZoneCountAsync(IEngineApi api, ZoneCountStart start)
    {
        try
        {
            await api.StartZoneCountAsync(start.DeviceId, start.ZoneIndex, start.LedsPerSecond);
        }
        catch
        {
            session.Ui.Run(() => preview().ForgetZoneCountStart(start));
            throw;
        }
    }
}
