using System.Diagnostics;
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
        var options = EngineOptions.Parse(args);
        using var instance = SingleInstance.Acquire(EngineRpc.EngineName);
        if (!instance.IsPrimary)
        {
            if (options.Exit)
            {
                instance.SignalPrimaryToExit();
            }
            else
            {
                instance.SignalPrimary();
            }

            return;
        }

        // No engine runs, so there is nothing to stop.
        if (options.Exit)
        {
            return;
        }

        // After the single-instance check, so a second start does not write to the log of the first.
        using var log = FileLog.Start("engine");
        ApplicationConfiguration.Initialize();

        var autoStart = RegistryAutoStart.ForCurrentProcess();
        autoStart.EnableOnFirstRun(options);
        autoStart.FollowMoveOnStart();

        var simulatedDevices = SwitchableDeviceProvider.For(new SimulatedDeviceProvider());
        using var openRgb = OpenRgbDeviceProvider.ForLocalServer();
        using var lighting = new LightingController([simulatedDevices, openRgb]);
        using var simulatedDevicesSwitch = new SimulatedDevicesSwitch(simulatedDevices, lighting);
        using var zoneLengths = new ZoneLengthKeeper(lighting);

        // On this thread, which pumps the tray messages and so also the power, session and clock events.
        using var powerEvents = new SystemPowerEvents();
        using var sessionEvents = new SystemSessionEvents();
        using var clockEvents = new SystemClockEvents(powerEvents);
        using var resumeRestorer = new ResumeRestorer(powerEvents, lighting.RestoreDevices, TimeProvider.System, ResumeRestorer.DefaultDelays);
        using var autoPause = new AutoPause(powerEvents, sessionEvents, lighting.SetPaused);
        var saveEvents = new SettingsSaveEvents();
        var profiles = new ProfileLibrary(new JsonProfileStore(JsonProfileStore.DefaultFilePath), lighting);
        // The host disposes them, before it saves for the last time.
        var programWatcher = new ProgramWatcher(profiles, lighting, new ProcessList(), TimeProvider.System);
        var profileTimer = new ProfileTimer(profiles, clockEvents, TimeProvider.System, programWatcher.HoldUntilProgramsStop);
        using var host = new EngineHost(
            new JsonSettingsStore(JsonSettingsStore.DefaultFilePath),
            lighting,
            new EngineIpcServer(
                EngineRpc.PipeName,
                () => new EngineApi(lighting, zoneLengths, profiles, simulatedDevicesSwitch, openRgb, saveEvents),
                MemoryTrimmer.Trim),
            simulatedDevices: simulatedDevicesSwitch,
            trimMemory: MemoryTrimmer.Trim,
            saveEvents: saveEvents,
            profileTriggers: [profileTimer, programWatcher],
            zoneLengths: zoneLengths,
            autoPause: autoPause);

        using var context = new TrayApplicationContext(host, autoStart, SettingsAppLauncher.NextToEngine(), profiles, openRgb, MemoryTrimmer.Trim);
        using (instance.ListenForActivation(context.RequestOpenSettings))
        using (instance.ListenForExit(context.RequestExit))
        {
            Application.Run(context);
        }

        Trace.TraceInformation("Engine stopped.");
    }
}
