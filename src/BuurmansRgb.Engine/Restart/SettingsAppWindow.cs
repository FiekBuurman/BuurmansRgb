using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Restart;

// The open settings app, which "Herstarten" closes so the new engine can open it again.
internal interface ISettingsAppWindow
{
    // Asks an open settings app to close; false when none was open.
    bool CloseIfOpen();
}

// Through the single-instance mutex and exit signal of the settings app (SingleInstance), as
// --exit does with the engine, but without creating the mutex when no settings app runs.
internal sealed class SettingsAppWindow(string instanceName = EngineRpc.SettingsAppName) : ISettingsAppWindow
{
    public bool CloseIfOpen() => SingleInstance.SignalExitIfRunning(instanceName);
}
