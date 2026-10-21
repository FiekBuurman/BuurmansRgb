using System.Diagnostics;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.Services;

// The programs that show a window now, as the .exe names "Aanzetten als dit programma start" takes:
// a game or app the user has open, without the background processes nobody would pick. The name
// comes from the process, so it also works for programs that run as administrator.
public static class WindowedPrograms
{
    public static IReadOnlyList<string> Find()
    {
        var own = Environment.ProcessId;
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id != own && process.MainWindowHandle != IntPtr.Zero && ProgramNames.Normalize(process.ProcessName) is { } name)
                    {
                        names.Add(name);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Ended while the list was read.
                }
            }
        }

        return [.. names];
    }
}
