using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BuurmansRgb.UI.Services;

// The processes of Windows for WindowedPrograms: which ones show a window, and the description in
// the exe file of each. Safe to call from any thread.
internal static partial class SystemProcesses
{
    private const uint QueryLimitedInformation = 0x1000;
    private const int ErrorInsufficientBuffer = 122;
    private const int MaxPath = 32_767;

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
    // such as one of another user or a protected one. Its path comes from QueryFullProcessImageName,
    // which needs no admin rights, also for a program that runs as administrator. Throws what
    // reading the file throws, such as FileNotFoundException.
    public static string? Description(int processId) =>
        ImagePath(processId) is { } path ? Descriptions.GetOrAdd(path, static path => FileVersionInfo.GetVersionInfo(path).FileDescription) : null;

    private static string? ImagePath(int processId)
    {
        using var process = OpenProcess(QueryLimitedInformation, inheritHandle: false, (uint)processId);
        if (process.IsInvalid)
        {
            return null;
        }

        var path = new char[260];
        while (true)
        {
            var length = (uint)path.Length;
            if (QueryFullProcessImageName(process, 0, path, ref length))
            {
                return new string(path, 0, (int)length);
            }

            if (Marshal.GetLastPInvokeError() != ErrorInsufficientBuffer || path.Length >= MaxPath)
            {
                return null;
            }

            path = new char[Math.Min(path.Length * 4, MaxPath)];
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, [Out] char[] path, ref uint length);
}
