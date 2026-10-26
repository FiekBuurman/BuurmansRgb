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

    // The process itself, also an engine exe under another name. Only when that is dotnet.exe,
    // for `dotnet BuurmansRgb.Engine.dll`, or unknown: the engine exe next to the engine's files,
    // as dotnet would start without the engine.
    internal static string EnginePath(string? processPath, string baseDirectory) =>
        processPath is null || IsDotnetHost(processPath)
            ? Path.Combine(baseDirectory, EngineExe)
            : processPath;

    public static EngineLauncher ForCurrentProcess() => new(CurrentEnginePath);

    private static bool IsDotnetHost(string processPath) =>
        string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase);

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
