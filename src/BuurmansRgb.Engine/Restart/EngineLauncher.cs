using System.ComponentModel;
using System.Diagnostics;

namespace BuurmansRgb.Engine.Restart;

// Starts a new engine for "Herstarten"; it waits until this one has exited (EngineOptions.Restarted).
internal interface IEngineLauncher
{
    // False when it could not start; the reason is logged.
    bool Start(IReadOnlyList<string> arguments);
}

internal sealed class EngineLauncher(string executablePath) : IEngineLauncher
{
    // The engine exe, also when it was started under another name or from another folder.
    public static string CurrentEnginePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "BuurmansRgb.Engine.exe");

    public static EngineLauncher ForCurrentProcess() => new(CurrentEnginePath);

    public bool Start(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(executablePath) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            Trace.TraceError(exception.ToString());
            return false;
        }
    }
}
