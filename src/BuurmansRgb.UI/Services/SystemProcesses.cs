using System.Collections.Concurrent;
using System.Diagnostics;
using BuurmansRgb.Contracts;

namespace BuurmansRgb.UI.Services;

// The processes of Windows for WindowedPrograms: which ones show a window, and the description in
// the exe file of each. Safe to call from any thread.
internal static class SystemProcesses
{
    // By the path of the exe: a file keeps its description, so a list that opens again only reads
    // the programs that started since.
    private static readonly ConcurrentDictionary<string, string?> Descriptions = new(StringComparer.OrdinalIgnoreCase);

    // The processes with a main window, except this app itself. The name and the title come from
    // the process, so this also works for programs that run as administrator.
    public static List<WindowedProcess> Windowed()
    {
        var own = Environment.ProcessId;
        var windowed = new List<WindowedProcess>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id != own && process.MainWindowHandle != IntPtr.Zero)
                    {
                        windowed.Add(new WindowedProcess(process.Id, process.ProcessName, process.MainWindowTitle));
                    }
                }
                catch (InvalidOperationException)
                {
                    // Ended while the list was read.
                }
            }
        }

        return windowed;
    }

    // The description in the exe file of the process (FileVersionInfo.FileDescription), such as
    // "Outlook" for olk.exe; null when the file has none, or when Windows does not open the process,
    // such as one of another user or a protected one. Its path comes from ProcessImageName, which
    // needs no admin rights, also for a program that runs as administrator; one for each call, as a
    // ProcessImageName is not thread-safe. Throws what reading the file throws, such as
    // FileNotFoundException.
    public static string? Description(int processId) =>
        new ProcessImageName().ReadPath((uint)processId) is { } path
            ? Descriptions.GetOrAdd(path, static path => FileVersionInfo.GetVersionInfo(path).FileDescription)
            : null;
}
