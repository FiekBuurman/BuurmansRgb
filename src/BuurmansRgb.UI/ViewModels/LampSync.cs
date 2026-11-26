namespace BuurmansRgb.UI.ViewModels;

// Keeps the lamps of the Lampen page in step with the engine: sends a lamp the user switched, and
// asks the lighting of all lamps again. A coordinator: it calls the engine through the session,
// and a failed refresh goes to its OnCallFailed. Used on the UI thread.
internal sealed class LampSync(LampsViewModel lamps, EngineSession session)
{
    // The kind of call in the call queue; with the device it makes the key that lets a newer switch
    // replace one that still waits.
    private const string OnCall = "on";

    // The engine reports the change through LightingChanged, after which the lamps show what it did.
    public void Switch(LampSwitch lamp) =>
        session.Post((OnCall, lamp.DeviceId), api => api.SetOnAsync(lamp.DeviceId, lamp.On));

    // A connection made again in the meantime asks once more when this one is done, so the lamps end
    // up showing what that engine has.
    public async Task RefreshAsync()
    {
        if (session.Current is not { } current)
        {
            return;
        }

        try
        {
            await lamps.RefreshAsync(() => current.Api.GetAllLightingAsync());
        }
        catch (Exception exception)
        {
            session.OnCallFailed(exception);
        }
    }
}
