using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BuurmansRgb.Engine.Programs;

// The exe name of a process by its id, as ProgramNames compares it ("eldenring.exe"): the file name
// of QueryFullProcessImageName, which needs no admin rights, also for a program that runs as
// administrator. Keeps its buffer between calls, so it is not thread-safe.
internal sealed partial class ProcessImageName
{
    private const uint QueryLimitedInformation = 0x1000;
    private const int ErrorAccessDenied = 5;
    private const int ErrorInsufficientBuffer = 122;
    private const int MaxPath = 32_767;

    private char[] _path = new char[260];

    // True with the exe name, or with null when Windows will never give it to us (access denied,
    // such as for the system's own processes). False for any other failure, which can pass: the
    // process may just have ended, or not be set up yet.
    public bool TryRead(uint processId, out string? name)
    {
        name = null;
        using var process = OpenProcess(QueryLimitedInformation, inheritHandle: false, processId);
        if (process.IsInvalid)
        {
            return Marshal.GetLastPInvokeError() == ErrorAccessDenied;
        }

        while (true)
        {
            var length = (uint)_path.Length;
            if (QueryFullProcessImageName(process, 0, _path, ref length))
            {
                var path = _path.AsSpan(0, (int)length);
                name = new string(path[(path.LastIndexOf('\\') + 1)..]);
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
