using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.AutoStart;
using BuurmansRgb.Engine.Hotkeys;
using BuurmansRgb.Engine.Ipc;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Programs;
using BuurmansRgb.Engine.Restart;
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

    // How long an engine that "Herstarten" started waits for the old one to exit. The old one
    // exits as with "Afsluiten", which takes a few seconds at most.
    private static readonly TimeSpan PreviousEngineTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PreviousEnginePollInterval = TimeSpan.FromMilliseconds(200);

    // Waits when disposed until OpenRGB got the work queued for it, such as the length a count gave
    // back, but at most ResizesOnExitTimeout: an OpenRGB that takes longer keeps the zone at its
    // longest. It always waits, as the count may have stopped another way before the counter goes,
    // such as when the settings app went along with the engine; without work it returns at once.
    internal static IDisposable WaitForResizesOnExit(OpenRgbDeviceProvider openRgb) =>
        new DisposeAction(() => openRgb.WaitUntilIdle(ResizesOnExitTimeout));

    [STAThread]
    private static int Main(string[] args)
    {
        var options = EngineOptions.Parse(args);

        // The step with admin rights of "Herstarten", next to the engine that runs: no instance,
        // log or tray. What it did is its exit code.
        if (options.RestartServices)
        {
            return RestartServices();
        }

        using var instance = options.Restarted is null
            ? SingleInstance.Acquire(EngineRpc.EngineName)
            : SingleInstance.AcquireWhenFree(EngineRpc.EngineName, PreviousEngineTimeout, PreviousEnginePollInterval);
        if (!instance.IsPrimary)
        {
            // The old engine did not exit in time; it keeps running, and this one stops quietly.
            if (options.Restarted is not null)
            {
                return 0;
            }

            if (options.Exit)
            {
                instance.SignalPrimaryToExit();
            }
            else
            {
                instance.SignalPrimary();
            }

            return 0;
        }

        // No engine runs, so there is nothing to stop.
        if (options.Exit)
        {
            return 0;
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

        // Disposed after the counter, so a length it gave back reaches OpenRGB before the provider closes.
        using var resizesSent = WaitForResizesOnExit(openRgb);

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
        // Disposed after the host, so the last change of an own effect is written at the exit.
        using var customEffects = new CustomEffectLibrary(new JsonCustomEffectStore(JsonCustomEffectStore.DefaultFilePath), lighting);
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
                () => new EngineApi(lighting, zoneLengths, profiles, simulatedDevicesSwitch, openRgb, saveEvents, autoPause: autoPause, profileTimer: profileTimer, hotkeys: hotkeys, customColors: customColors, zoneCounter: zoneCounter, customEffects: customEffects),
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
            customColors: customColors,
            customEffects: customEffects);

        using var context = new TrayApplicationContext(
            host,
            autoStart,
            SettingsAppLauncher.NextToEngine(),
            profiles,
            lighting,
            openRgb,
            hotkeys,
            log.FilePath,
            MemoryTrimmer.Trim,
            EngineRestarter.ForCurrentProcess(),
            options.Restarted,
            options.OpenSettings);
        using (instance.ListenForActivation(context.RequestOpenSettings))
        using (instance.ListenForExit(context.RequestExit))
        {
            Application.Run(context);
        }

        Trace.TraceInformation("Engine stopped.");
        return 0;
    }

    // Any failure ends as a failed step, which the engine reports; an exception would only give
    // Windows' crash dialog and an exit code the engine reads as failed anyway. The step writes no
    // log (no file as admin), so the cause of the first failure goes along in the exit code and
    // the engine logs it.
    private static int RestartServices()
    {
        try
        {
            var restarter = new ServiceRestarter(new WindowsServiceManager());
            return restarter.Restart().ToExitCode(restarter.Cause);
        }
        catch (Exception exception)
        {
            return ServicesRestartReport.Failed.ToExitCode(ServicesRestartCause.From(ServicesRestartStep.Step, exception));
        }
    }
}
