namespace BuurmansRgb.Core;

// A provider whose devices can lose their setup, for example after sleep, when many devices go
// back to an effect of their own. RestoreDevices sets them up again to take colors from SetLeds;
// the engine sends every color again right after. It may finish in the background, and a dynamic
// provider then raises DevicesChanged. Like SetLeds it is called with the engine's lock held, so it
// must not wait.
public interface IRestorableDeviceProvider : IDeviceProvider
{
    void RestoreDevices();
}
