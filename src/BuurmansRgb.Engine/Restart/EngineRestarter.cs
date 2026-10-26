namespace BuurmansRgb.Engine.Restart;

// What "Herstarten" did before this engine exits: the report goes to the new engine, which tells
// it once it runs.
internal sealed record EngineRestartResult(ServicesRestartReport Report, bool NewEngineStarted, bool SettingsAppClosed);

// "Herstarten" in the tray: restarts OpenRGB and PawnIO (one UAC prompt), closes an open settings
// app and starts a new engine with --restarted, which waits until this one has exited and then
// opens the settings app again. The caller exits this engine once the new one started; when it
// did not start, this one keeps running.
internal sealed class EngineRestarter(IServicesRestart services, ISettingsAppWindow settingsApp, IEngineLauncher engineLauncher)
{
    public static EngineRestarter ForCurrentProcess() =>
        new(ElevatedServicesRestart.ForCurrentProcess(), new SettingsAppWindow(), EngineLauncher.ForCurrentProcess());

    public async Task<EngineRestartResult> RestartAsync(CancellationToken ct)
    {
        var report = await services.RestartAsync(ct);
        var settingsAppClosed = settingsApp.CloseIfOpen();
        List<string> arguments = [EngineOptions.RestartedArgument, report.ToArgument()];
        if (settingsAppClosed)
        {
            arguments.Add(EngineOptions.OpenSettingsArgument);
        }

        var started = engineLauncher.Start(arguments);
        return new(report, started, settingsAppClosed);
    }
}
