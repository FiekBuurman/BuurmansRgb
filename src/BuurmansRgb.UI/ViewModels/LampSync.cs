using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.ViewModels;

// Keeps the lamps of the Lampen page in step with the engine: sends a lamp the user switched, and
// asks the lighting of all lamps again. connection gives the current connection, or null; a failed
// refresh goes to onCallFailed. Used on the UI thread.
internal sealed class LampSync(LampsViewModel lamps, CoalescingCallQueue calls, Func<IEngineConnection?> connection, Action<Exception> onCallFailed)
{
    // The kind of call in the call queue; with the device it makes the key that lets a newer switch
    // replace one that still waits.
    private const string OnCall = "on";

    // The engine reports the change through LightingChanged, after which the lamps show what it did.
    public void Switch(LampSwitch lamp)
    {
        if (connection() is { } current)
        {
            calls.Post((OnCall, lamp.DeviceId), () => current.Api.SetOnAsync(lamp.DeviceId, lamp.On));
        }
    }

    // A connection made again in the meantime asks once more when this one is done, so the lamps end
    // up showing what that engine has.
    public async Task RefreshAsync()
    {
        if (connection() is not { } current)
        {
            return;
        }

        try
        {
            await lamps.RefreshAsync(() => current.Api.GetAllLightingAsync());
        }
        catch (Exception exception)
        {
            onCallFailed(exception);
        }
    }
}
