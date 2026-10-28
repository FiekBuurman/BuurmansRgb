using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BuurmansRgb.Contracts;

// The exe of a process by its id, from QueryFullProcessImageName, which needs no admin rights, also
// for a program that runs as administrator: for the engine its exe name as ProgramNames compares it
// ("eldenring.exe"), for the settings app its whole path. Shared so both read it the same way. Keeps
// its buffer between calls, so one instance is not thread-safe.
public sealed partial class ProcessImageName
{
    // The access that QueryFullProcessImageName needs, also for a process of an administrator.
    public const uint QueryLimitedInformation = 0x1000;

    private const int ErrorAccessDenied = 5;
    private const int ErrorInsufficientBuffer = 122;
    private const int MaxPath = 32_767;

    private char[] _path = new char[260];

    // True with the exe name, or with null when Windows will never give it to us (access denied,
    // such as for the system's own processes). False for any other failure, which can pass: the
    // process may just have ended, or not be set up yet.
    public bool TryRead(uint processId, out string? name)
    {
        var read = TryReadPath(processId, out var path);
        name = path.IsEmpty ? null : new string(path[(path.LastIndexOf('\\') + 1)..]);
        return read;
    }

    // The whole path of the exe, or null when it cannot be read, for whatever reason.
    public string? ReadPath(uint processId) => TryReadPath(processId, out var path) && !path.IsEmpty ? new string(path) : null;

    // A handle to the process with access, for the engine's ProcessList too, so there is one
    // declaration; invalid when Windows does not open it (Marshal.GetLastPInvokeError says why).
    public static SafeProcessHandle Open(uint access, uint processId) => OpenProcess(access, inheritHandle: false, processId);

    // As TryRead, with the path in the buffer, empty when there is none.
    private bool TryReadPath(uint processId, out ReadOnlySpan<char> path)
    {
        path = [];
        using var process = Open(QueryLimitedInformation, processId);
        if (process.IsInvalid)
        {
            return Marshal.GetLastPInvokeError() == ErrorAccessDenied;
        }

        while (true)
        {
            var length = (uint)_path.Length;
            if (QueryFullProcessImageName(process, 0, _path, ref length))
            {
                path = _path.AsSpan(0, (int)length);
                return true;
            }

            var error = Marshal.GetLastPInvokeError();
            if (error != ErrorInsufficientBuffer || _path.Length >= MaxPath)
            {
                return error == ErrorAccessDenied;
            }

            _path = new char[Math.Min(_path.Length * 4, MaxPath)];
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, [Out] char[] path, ref uint length);
}
