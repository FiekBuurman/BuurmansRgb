using System.Runtime.InteropServices;
using BuurmansRgb.Contracts;
using Microsoft.Win32.SafeHandles;

namespace BuurmansRgb.Engine.Programs;

// The running programs by their process ids (EnumProcesses) and the exe name of each new process.
// No admin rights needed, also for programs that run as administrator. Measured with about 470
// processes: EnumProcesses takes under 0.1 ms, where a Toolhelp snapshot or
// Process.GetProcessesByName takes 5 to 7 ms, as they also read every thread of every process. So
// the name of a process is looked up once, when its id first shows, and kept until it is gone. A
// call allocates nothing while no new process started and none of the names runs. Not thread-safe.
//
// Windows hands out a process id again soon after its process is gone. A process whose name is
// watched therefore keeps an open handle here, so its id cannot go to another process, and it
// counts as running until that handle says it ended. The other way round stays a known risk: a
// process that ends and whose id goes to a watched program between two calls keeps the old name,
// so that program is missed until it starts again.
internal sealed partial class ProcessList : IRunningPrograms
{
    private const uint Synchronize = 0x0010_0000;
    private const uint WaitObject0 = 0;
    private const uint StillActive = 259;

    // The idle process has id 0 and can never be opened.
    private const uint IdleProcessId = 0;

    // Calls in which the name of a process may fail to read before it counts as one without a name,
    // like a process Windows denies access to.
    private const int MaxNameAttempts = 3;

    private static readonly IReadOnlySet<string> None = new HashSet<string>(ProgramNames.Comparer);

    private readonly Dictionary<uint, ProcessEntry> _processes = [];
    private readonly ProcessImageName _names = new();
    private uint[] _ids = new uint[1024];
    private int _round;

    public IReadOnlySet<string> Find(IReadOnlySet<string> names)
    {
        var watched = names is HashSet<string> set && set.Comparer == ProgramNames.Comparer
            ? set
            : new HashSet<string>(names, ProgramNames.Comparer);
        HashSet<string>? running = null;
        var count = ReadIds();
        _round++;
        for (var i = 0; i < count; i++)
        {
            var id = _ids[i];
            if (id == IdleProcessId)
            {
                continue;
            }

            if (!_processes.TryGetValue(id, out var process))
            {
                process = new ProcessEntry();
                _processes.Add(id, process);
            }

            process.Round = _round;
            if (!process.HasName)
            {
                // When the name cannot be read for now, the next call tries again, but not forever:
                // a process that can be opened but never named (seen on GitHub's Windows runners)
                // would cost a handle every call.
                process.NameAttempts++;
                if (_names.TryRead(id, out var newName))
                {
                    process.SetName(newName);
                }
                else if (process.NameAttempts >= MaxNameAttempts)
                {
                    process.SetName(null);
                }
                else
                {
                    continue;
                }
            }

            if (process.Name is { } name && watched.TryGetValue(name, out var watchedName))
            {
                if (process.IsRunning(id))
                {
                    (running ??= new HashSet<string>(ProgramNames.Comparer)).Add(watchedName);
                }
            }
            else
            {
                // No longer watched: its id may go to another process again.
                process.Release();
            }
        }

        foreach (var (id, process) in _processes)
        {
            if (process.Round != _round)
            {
                process.Release();
                _processes.Remove(id);
            }
        }

        return running ?? None;
    }

    // The ids of all processes; a full buffer can mean there are more, so it grows and reads again.
    private int ReadIds()
    {
        while (true)
        {
            var size = (uint)_ids.Length * sizeof(uint);
            if (!EnumProcesses(_ids, size, out var used))
            {
                throw new InvalidOperationException($"The process list could not be read (error {Marshal.GetLastPInvokeError()}).");
            }

            if (used < size)
            {
                return (int)(used / sizeof(uint));
            }

            _ids = new uint[_ids.Length * 2];
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "K32EnumProcesses", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumProcesses([Out] uint[] processIds, uint size, out uint used);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(SafeProcessHandle process, out uint exitCode);

    [LibraryImport("kernel32.dll")]
    private static partial uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

    // One process id as last seen: its exe name, or null when it cannot be read (such as the
    // system's own), and while its name is watched a handle that keeps the id.
    private sealed class ProcessEntry
    {
        private SafeProcessHandle? _handle;
        private bool _ended;

        public string? Name { get; private set; }

        // False until the name was read, or given up on after MaxNameAttempts.
        public bool HasName { get; private set; }

        public int NameAttempts { get; set; }

        public int Round { get; set; }

        public void SetName(string? name)
        {
            Name = name;
            HasName = true;
        }

        // A process that ended stays in the list while a program still holds a handle to it, such
        // as a game launcher, or this one. It has ended once it has an exit code: Windows sets that
        // right away, but signals the handle only once all its threads are gone, which can take a
        // while under load (Process.HasExited also goes by the exit code). The handle stays open
        // until it is signalled, so the id cannot go to another process while this one is still on
        // its way out. A process that cannot be opened to wait on counts as running while it is in
        // the list.
        public bool IsRunning(uint processId)
        {
            if (!_ended)
            {
                _handle ??= ProcessImageName.Open(Synchronize | ProcessImageName.QueryLimitedInformation, processId);
                if (_handle.IsInvalid)
                {
                    return true;
                }

                _ended = IsSignaled(_handle) || (GetExitCodeProcess(_handle, out var exitCode) && exitCode != StillActive);
            }

            if (_ended && _handle is not null && IsSignaled(_handle))
            {
                Release();
            }

            return !_ended;
        }

        public void Release()
        {
            _handle?.Dispose();
            _handle = null;
        }

        private static bool IsSignaled(SafeProcessHandle handle) => WaitForSingleObject(handle, 0) == WaitObject0;
    }
}
