using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Hotkeys;
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
    // How long the engine waits on the way out for OpenRGB to get a zone length back after a count.
    private static readonly TimeSpan ResizesOnExitTimeout = TimeSpan.FromSeconds(2);

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
        using var simulatedDevicesSwitch = new SimulatedDevicesSwitch(simulatedDevices, lighting, openRgb);
        using var zoneLengths = new ZoneLengthKeeper(lighting);

        // Disposed after the counter: the length it gives back reaches OpenRGB before the provider
        // closes, unless OpenRGB takes longer than this.
        using var resizesSent = new DisposeAction(() => openRgb.WaitUntilIdle(ResizesOnExitTimeout));

        // Disposed before the lengths and the lighting, so a zone that is counted gets its length back.
        using var zoneCounter = new ZoneCounter(lighting, zoneLengths);

        // On this thread, which pumps the tray messages and so also the power, session, screen,
        // clock and foreground events, and the screen saver through the foreground.
        using var powerEvents = new SystemPowerEvents();
        using var sessionEvents = new SystemSessionEvents();
        using var displayEvents = new SystemDisplayEvents();
        using var clockEvents = new SystemClockEvents(powerEvents);
        using var foregroundHook = new ForegroundHook();
        using var foregroundWindow = new SystemForegroundWindow(foregroundHook);
        using var screenSaverEvents = new SystemScreenSaverEvents(foregroundHook);
        using var resumeRestorer = new ResumeRestorer(powerEvents, lighting.RestoreDevices, TimeProvider.System, ResumeRestorer.DefaultDelays);
        using var autoPause = new AutoPause(powerEvents, sessionEvents, displayEvents, screenSaverEvents, lighting.SetPaused);
        var saveEvents = new SettingsSaveEvents();
        var hotkeys = new AllOnOffHotkeys();
        var customColors = new CustomColorList();
        var profiles = new ProfileLibrary(new JsonProfileStore(JsonProfileStore.DefaultFilePath), lighting);
        // Disposed after the host, which stops listening to it first.
        using var settingsFile = new SettingsFileWatcher(JsonSettingsStore.DefaultFilePath);
        // The host disposes them, before it saves for the last time.
        var programWatcher = new ProgramWatcher(profiles, lighting, new ProcessList(), autoPause, foregroundWindow, TimeProvider.System);
        var profileTimer = new ProfileTimer(profiles, clockEvents, TimeProvider.System, programWatcher.HoldUntilProgramsStop, resumeRestorer);
        using var host = new EngineHost(
            new JsonSettingsStore(JsonSettingsStore.DefaultFilePath),
            lighting,
            new EngineIpcServer(
                EngineRpc.PipeName,
                () => new EngineApi(lighting, zoneLengths, profiles, simulatedDevicesSwitch, openRgb, saveEvents, autoPause: autoPause, profileTimer: profileTimer, hotkeys: hotkeys, customColors: customColors, zoneCounter: zoneCounter),
                MemoryTrimmer.Trim),
            simulatedDevices: simulatedDevicesSwitch,
            trimMemory: MemoryTrimmer.Trim,
            saveEvents: saveEvents,
            profileTriggers: [profileTimer, programWatcher],
            zoneLengths: zoneLengths,
            autoPause: autoPause,
            profiles: profiles,
            profileTimer: profileTimer,
            hotkeys: hotkeys,
            settingsFileEvents: settingsFile,
            customColors: customColors);

        using var context = new TrayApplicationContext(host, autoStart, SettingsAppLauncher.NextToEngine(), profiles, lighting, openRgb, hotkeys, log.FilePath, MemoryTrimmer.Trim);
        using (instance.ListenForActivation(context.RequestOpenSettings))
        using (instance.ListenForExit(context.RequestExit))
        {
            Application.Run(context);
        }

        Trace.TraceInformation("Engine stopped.");
    }
}
