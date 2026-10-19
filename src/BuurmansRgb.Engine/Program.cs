using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Ipc;
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

        var lighting = new LightingController([new SimulatedDeviceProvider()]);
        using var host = new EngineHost(
            new JsonSettingsStore(JsonSettingsStore.DefaultFilePath),
            lighting,
            new EngineIpcServer(EngineRpc.PipeName, lighting));

        using var context = new TrayApplicationContext(host, RegistryAutoStart.ForCurrentProcess(), SettingsAppLauncher.NextToEngine());
        using (instance.ListenForActivation(context.RequestOpenSettings))
        {
            Application.Run(context);
        }
    }
}
