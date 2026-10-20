using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Programs;
using BuurmansRgb.Engine.Settings;
using BuurmansRgb.Engine.SettingsApp;
using BuurmansRgb.Engine.Tray;
using BuurmansRgb.Providers.OpenRgb;
using BuurmansRgb.Providers.Simulated;

namespace BuurmansRgb.Engine;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var instance = SingleInstance.Acquire(EngineRpc.EngineName);
        if (!instance.IsPrimary)
        {
            instance.SignalPrimary();
            return;
        }

        ApplicationConfiguration.Initialize();

        var autoStart = RegistryAutoStart.ForCurrentProcess();
        autoStart.EnableOnFirstRun(EngineOptions.Parse(args));

        var simulatedDevices = SwitchableDeviceProvider.For(new SimulatedDeviceProvider());
        using var openRgb = OpenRgbDeviceProvider.ForLocalServer();
        using var lighting = new LightingController([simulatedDevices, openRgb]);
        using var simulatedDevicesSwitch = new SimulatedDevicesSwitch(simulatedDevices, lighting);

        // On this thread, which pumps the tray messages and so also the power and clock events.
        using var powerEvents = new SystemPowerEvents();
        using var clockEvents = new SystemClockEvents(powerEvents);
        using var resumeRestorer = new ResumeRestorer(powerEvents, lighting.RestoreDevices, TimeProvider.System, ResumeRestorer.DefaultDelays);
        var saveEvents = new SettingsSaveEvents();
        var profiles = new ProfileLibrary(new JsonProfileStore(JsonProfileStore.DefaultFilePath), lighting);
        // The host disposes them, before it saves for the last time.
        var profileTimer = new ProfileTimer(profiles, clockEvents, TimeProvider.System);
        var programWatcher = new ProgramWatcher(profiles, lighting, new ProcessList(), TimeProvider.System);
        using var host = new EngineHost(
            new JsonSettingsStore(JsonSettingsStore.DefaultFilePath),
            lighting,
            new EngineIpcServer(
                EngineRpc.PipeName,
                () => new EngineApi(lighting, profiles, simulatedDevicesSwitch, openRgb, saveEvents),
                MemoryTrimmer.Trim),
            simulatedDevices: simulatedDevicesSwitch,
            trimMemory: MemoryTrimmer.Trim,
            saveEvents: saveEvents,
            profileTriggers: [profileTimer, programWatcher]);

        using var context = new TrayApplicationContext(host, autoStart, SettingsAppLauncher.NextToEngine(), profiles, openRgb);
        using (instance.ListenForActivation(context.RequestOpenSettings))
        {
            Application.Run(context);
        }
    }
}
