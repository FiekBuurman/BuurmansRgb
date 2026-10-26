using System.ComponentModel;
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

    // The name the PawnIO setup is thought to give its driver: a guess until it is checked on a pc
    // with PawnIO, so FindPawnIo also looks for a driver with "PawnIO" in its name. Where the guess
    // comes from (LibreHardwareMonitor, FanControl, winget): docs/openrgb-meeleveren.md, "PawnIO".
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

    // The first failure of the last Restart, which goes along in the exit code; none when all went well.
    public ServicesRestartCause Cause { get; private set; }

    public ServicesRestartReport Restart()
    {
        Cause = default;
        var openRgb = services.Exists(OpenRgbService) ? OpenRgbService : null;
        var pawnIo = FindPawnIo(services);
        Trace.TraceInformation($"Restarting the services: OpenRGB {(openRgb is null ? "not installed" : "found")}, PawnIO {(pawnIo is null ? "not found" : $"as '{pawnIo}'")}.");

        var openRgbStopped = openRgb is null || TryStop(openRgb, OpenRgbStopTimeout, ServicesRestartStep.StopOpenRgb);

        var pawnIoOutcome = ServiceOutcome.NotInstalled;
        if (pawnIo is not null)
        {
            // OpenRGB that still runs holds the driver, so it would not stop either.
            pawnIoOutcome = !openRgbStopped ? ServiceOutcome.SkippedForOpenRgb
                : !TryStop(pawnIo, PawnIoTimeout, ServicesRestartStep.StopPawnIo) ? ServiceOutcome.NotStopped
                : TryStart(pawnIo, PawnIoTimeout, ServicesRestartStep.StartPawnIo) ? ServiceOutcome.Restarted : ServiceOutcome.Failed;
        }

        var openRgbOutcome = ServiceOutcome.NotInstalled;
        if (openRgb is not null)
        {
            // Started also when it did not stop, so it runs afterwards whatever happened.
            var started = TryStart(openRgb, OpenRgbStartTimeout, ServicesRestartStep.StartOpenRgb);
            openRgbOutcome = openRgbStopped && started ? ServiceOutcome.Restarted : ServiceOutcome.Failed;
        }

        var report = new ServicesRestartReport(openRgbOutcome, pawnIoOutcome);
        Trace.TraceInformation($"Restarted the services: OpenRGB {report.OpenRgb}, PawnIO {report.PawnIo}.");
        return report;
    }

    private bool TryStop(string name, TimeSpan timeout, ServicesRestartStep step)
    {
        try
        {
            services.Stop(name, timeout);
            return true;
        }
        catch (Exception exception) when (exception is ServiceControlException or Win32Exception)
        {
            Trace.TraceWarning($"Could not stop {name}: {exception.Message}");
            Remember(step, exception);
            return false;
        }
    }

    private bool TryStart(string name, TimeSpan timeout, ServicesRestartStep step)
    {
        try
        {
            services.Start(name, timeout);
            return true;
        }
        catch (Exception exception) when (exception is ServiceControlException or Win32Exception)
        {
            Trace.TraceWarning($"Could not start {name}: {exception.Message}");
            Remember(step, exception);
            return false;
        }
    }

    // Only the first: a later failure, such as OpenRGB that does not start after it did not stop,
    // often follows from it.
    private void Remember(ServicesRestartStep step, Exception exception)
    {
        if (Cause.Step == ServicesRestartStep.None)
        {
            Cause = ServicesRestartCause.From(step, exception);
        }
    }
}
