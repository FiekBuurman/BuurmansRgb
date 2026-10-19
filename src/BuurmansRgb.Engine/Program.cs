using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Engine.SettingsApp;
using BuurmansRgb.Engine.Tray;
using BuurmansRgb.Providers.OpenRgb;
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

        var simulatedDevices = SwitchableDeviceProvider.For(new SimulatedDeviceProvider());
        using var openRgb = OpenRgbDeviceProvider.ForLocalServer();
        using var lighting = new LightingController([simulatedDevices, openRgb]);

        // On this thread, which pumps the tray messages and so also the power events.
        using var powerEvents = new SystemPowerEvents();
        using var resumeRestorer = new ResumeRestorer(powerEvents, lighting.RestoreDevices, TimeProvider.System, ResumeRestorer.DefaultDelays);
        using var host = new EngineHost(
            new JsonSettingsStore(JsonSettingsStore.DefaultFilePath),
            lighting,
            new EngineIpcServer(EngineRpc.PipeName, lighting, MemoryTrimmer.Trim),
            simulatedDevices: simulatedDevices,
            trimMemory: MemoryTrimmer.Trim);

        using var context = new TrayApplicationContext(host, RegistryAutoStart.ForCurrentProcess(), SettingsAppLauncher.NextToEngine());
        using (instance.ListenForActivation(context.RequestOpenSettings))
        {
            Application.Run(context);
        }
    }
}
