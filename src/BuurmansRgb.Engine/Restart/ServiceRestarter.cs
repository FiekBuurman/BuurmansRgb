using System.Diagnostics;

namespace BuurmansRgb.Engine.Restart;

// The step with admin rights of "Herstarten" (the engine with --restart-services): stops OpenRGB,
// stops PawnIO, starts PawnIO and starts OpenRGB, so OpenRGB lets go of PawnIO before it stops and
// finds it running again when it starts. PawnIO is best effort: another program (FanControl,
// LibreHardwareMonitor) may hold the driver, and then it stays as it is; OpenRGB always starts
// again.
internal sealed class ServiceRestarter(IServiceManager services)
{
    // The name the installer gives the service (installer/BuurmansRgb.iss, OpenRgbService).
    public const string OpenRgbService = "OpenRGB";

    // The name the PawnIO setup is thought to give its driver; not checked on a pc with PawnIO yet,
    // so FindPawnIo also looks for a driver with "PawnIO" in its name.
    public const string PawnIoService = "PawnIO";

    public static TimeSpan OpenRgbStopTimeout { get; } = TimeSpan.FromSeconds(30);

    public static TimeSpan OpenRgbStartTimeout { get; } = TimeSpan.FromSeconds(30);

    public static TimeSpan PawnIoTimeout { get; } = TimeSpan.FromSeconds(15);

    // At most this long, so the engine knows how long to wait for the step.
    public static TimeSpan MaxDuration => OpenRgbStopTimeout + OpenRgbStartTimeout + PawnIoTimeout + PawnIoTimeout;

    // The service of PawnIO: by its name, or else the first kernel driver with "PawnIO" in its name.
    public static string? FindPawnIo(IServiceManager services) =>
        services.Exists(PawnIoService) ? PawnIoService : services.FindDriver(PawnIoService);

    // Whether any of the two is on this pc; without them the engine asks no admin rights.
    public static bool AnyInstalled(IServiceManager services) =>
        services.Exists(OpenRgbService) || FindPawnIo(services) is not null;

    public ServicesRestartReport Restart()
    {
        var openRgb = services.Exists(OpenRgbService) ? OpenRgbService : null;
        var pawnIo = FindPawnIo(services);
        Trace.TraceInformation($"Restarting the services: OpenRGB {(openRgb is null ? "not installed" : "found")}, PawnIO {(pawnIo is null ? "not found" : $"as '{pawnIo}'")}.");

        var openRgbStopped = openRgb is null || TryStop(openRgb, OpenRgbStopTimeout);

        var pawnIoOutcome = ServiceOutcome.NotInstalled;
        if (pawnIo is not null)
        {
            // OpenRGB that still runs holds the driver, so it would not stop either.
            pawnIoOutcome = !openRgbStopped || !TryStop(pawnIo, PawnIoTimeout)
                ? ServiceOutcome.NotStopped
                : TryStart(pawnIo, PawnIoTimeout) ? ServiceOutcome.Restarted : ServiceOutcome.Failed;
        }

        var openRgbOutcome = ServiceOutcome.NotInstalled;
        if (openRgb is not null)
        {
            // Started also when it did not stop, so it runs afterwards whatever happened.
            var started = TryStart(openRgb, OpenRgbStartTimeout);
            openRgbOutcome = openRgbStopped && started ? ServiceOutcome.Restarted : ServiceOutcome.Failed;
        }

        var report = new ServicesRestartReport(openRgbOutcome, pawnIoOutcome);
        Trace.TraceInformation($"Restarted the services: OpenRGB {report.OpenRgb}, PawnIO {report.PawnIo}.");
        return report;
    }

    private bool TryStop(string name, TimeSpan timeout)
    {
        try
        {
            services.Stop(name, timeout);
            return true;
        }
        catch (Exception exception) when (exception is ServiceControlException or System.ComponentModel.Win32Exception)
        {
            Trace.TraceWarning($"Could not stop {name}: {exception.Message}");
            return false;
        }
    }

    private bool TryStart(string name, TimeSpan timeout)
    {
        try
        {
            services.Start(name, timeout);
            return true;
        }
        catch (Exception exception) when (exception is ServiceControlException or System.ComponentModel.Win32Exception)
        {
            Trace.TraceWarning($"Could not start {name}: {exception.Message}");
            return false;
        }
    }
}
