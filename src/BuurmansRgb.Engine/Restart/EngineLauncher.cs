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
    private const string EngineExe = "BuurmansRgb.Engine.exe";

    // The engine exe this process runs, also from another folder.
    public static string CurrentEnginePath => EnginePath(Environment.ProcessPath, AppContext.BaseDirectory);

    // The process itself when it is the engine exe; otherwise, such as dotnet.exe for
    // `dotnet BuurmansRgb.Engine.dll`, the engine exe next to the engine's files.
    internal static string EnginePath(string? processPath, string baseDirectory) =>
        processPath is not null && string.Equals(Path.GetFileName(processPath), EngineExe, StringComparison.OrdinalIgnoreCase)
            ? processPath
            : Path.Combine(baseDirectory, EngineExe);

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
