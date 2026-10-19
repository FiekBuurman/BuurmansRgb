using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Engine.SettingsApp;
using BuurmansRgb.Engine.Tray;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instance = SingleInstance.Acquire(SingleInstance.EngineName);
        if (!instance.IsPrimary)
        {
            instance.SignalPrimary();
            return;
        }

        ApplicationConfiguration.Initialize();

        var host = new EngineHost(
            new JsonSettingsStore(JsonSettingsStore.DefaultFilePath),
            new LightingController([new SimulatedDeviceProvider()]));

        using var context = new TrayApplicationContext(host, RegistryAutoStart.ForCurrentProcess(), SettingsAppLauncher.NextToEngine());
        instance.ListenForActivation(context.RequestOpenSettings);

        Application.Run(context);
    }
}
