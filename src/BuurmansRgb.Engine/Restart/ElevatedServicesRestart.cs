using System.ComponentModel;
using System.Diagnostics;

namespace BuurmansRgb.Engine.Restart;

// Restarts OpenRGB and PawnIO for the engine, which runs as a normal user.
internal interface IServicesRestart
{
    // Never throws, apart from a cancelled ct.
    Task<ServicesRestartReport> RestartAsync(CancellationToken ct);
}

// Starts the engine once more with --restart-services through UAC ("runas", one prompt) and reads
// what it did from its exit code. Without OpenRGB and PawnIO on this pc it asks nothing. The UAC
// prompt and the wait run off the tray thread, so the tray keeps working meanwhile.
internal sealed class ElevatedServicesRestart(string enginePath, IServiceManager services, Func<ProcessStartInfo, TimeSpan, CancellationToken, Task<int>>? runElevated = null)
    : IServicesRestart
{
    // ERROR_CANCELLED: the user said no to the UAC prompt.
    private const int ErrorCancelled = 1223;

    // How long the engine waits for the step: the time-outs of the services, and a margin for UAC.
    public static TimeSpan StepTimeout { get; } = ServiceRestarter.MaxDuration + TimeSpan.FromSeconds(30);

    private readonly Func<ProcessStartInfo, TimeSpan, CancellationToken, Task<int>> _runElevated = runElevated ?? RunElevatedAsync;

    public static ElevatedServicesRestart ForCurrentProcess() => new(EngineLauncher.CurrentEnginePath, new WindowsServiceManager());

    public async Task<ServicesRestartReport> RestartAsync(CancellationToken ct)
    {
        if (!AnyInstalled())
        {
            Trace.TraceInformation("OpenRGB and PawnIO are not installed; restarting only the engine.");
            return ServicesRestartReport.NothingInstalled;
        }

        var startInfo = new ProcessStartInfo(enginePath, EngineOptions.RestartServicesArgument)
        {
            UseShellExecute = true,
            Verb = "runas",
        };
        try
        {
            var code = await Task.Run(() => _runElevated(startInfo, StepTimeout, ct), ct);
            var report = ServicesRestartReport.FromExitCode(code);
            Trace.TraceInformation($"The restart of the services ended with 0x{code:X8}: OpenRGB {report.OpenRgb}, PawnIO {report.PawnIo}.");
            return report;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ErrorCancelled)
        {
            Trace.TraceInformation("The UAC prompt to restart the services was declined.");
            return ServicesRestartReport.Declined;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or OperationCanceledException or TimeoutException)
        {
            Trace.TraceError(exception.ToString());
            return ServicesRestartReport.Failed;
        }
    }

    // A failed look-up asks UAC anyway: the step itself finds out what is there.
    private bool AnyInstalled()
    {
        try
        {
            return ServiceRestarter.AnyInstalled(services);
        }
        catch (Win32Exception exception)
        {
            Trace.TraceWarning($"Could not look up the services: {exception.Message}");
            return true;
        }
    }

    // ShellExecuteEx gives a handle to the elevated process that can wait for it and read its exit
    // code. A step that takes longer than the timeout is left to finish by itself.
    private static async Task<int> RunElevatedAsync(ProcessStartInfo startInfo, TimeSpan timeout, CancellationToken ct)
    {
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The restart of the services did not start.");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"The restart of the services took longer than {timeout.TotalSeconds:0} s.");
        }

        return process.ExitCode;
    }
}
