using System.Diagnostics;
using System.Reflection;

namespace BuurmansRgb.Contracts;

// Writes what the engine and the settings app trace (Trace.TraceError and the like) to a log file in
// %AppData%\BuurmansRgb\logs, so a problem on a pc without a debugger or DebugView still leaves a
// trace. One line per message with the time and the level. A message that repeats the one before,
// such as "OpenRGB is not reachable" every 5 seconds, is counted instead of written again. Past
// MaxBytes the file moves to "<name>.old.log" and a new one starts, so the logs stay small.
public sealed class FileLog : TraceListener
{
    public const long DefaultMaxBytes = 1_000_000;

    private readonly Lock _gate = new();
    private readonly string _path;
    private readonly TimeProvider _clock;
    private readonly long _maxBytes;
    private StreamWriter? _writer;
    private string? _lastMessage;
    private int _repeats;

    public FileLog(string path, TimeProvider clock, long maxBytes = DefaultMaxBytes)
    {
        _path = path;
        _clock = clock;
        _maxBytes = maxBytes;
        _writer = Open();
    }

    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BuurmansRgb",
        "logs");

    public string OldPath => Path.ChangeExtension(_path, ".old.log");

    // Adds a log named name ("engine", "ui") to the trace listeners, notes the start and also logs
    // exceptions nobody handled. A log that cannot be opened (a folder without write access) writes
    // nothing; the app runs the same without it.
    public static FileLog Start(string name)
    {
        var log = new FileLog(Path.Combine(DefaultDirectory, name + ".log"), TimeProvider.System);
        Trace.Listeners.Add(log);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Trace.TraceError($"Unhandled exception: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) => Trace.TraceError($"Unobserved task exception: {e.Exception}");
        var assembly = Assembly.GetEntryAssembly()?.GetName();
        Trace.TraceInformation($"{assembly?.Name} {assembly?.Version} started (Windows {Environment.OSVersion.Version}, .NET {Environment.Version}).");
        return log;
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message) =>
        WriteEntry(eventType.ToString(), message ?? "");

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args) =>
        WriteEntry(eventType.ToString(), args is { Length: > 0 } && format is not null ? string.Format(format, args) : format ?? "");

    public override void Write(string? message) => WriteEntry("Trace", message ?? "");

    public override void WriteLine(string? message) => WriteEntry("Trace", message ?? "");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_gate)
            {
                WriteRepeats();
                _writer?.Dispose();
                _writer = null;
            }
        }

        base.Dispose(disposing);
    }

    private void WriteEntry(string level, string message)
    {
        lock (_gate)
        {
            var text = $"{level} {message}";
            if (text == _lastMessage)
            {
                _repeats++;
                return;
            }

            WriteRepeats();
            _lastMessage = text;
            WriteLineLocked($"{_clock.GetLocalNow():yyyy-MM-dd HH:mm:ss.fff} {level,-11} {message}");
        }
    }

    private void WriteRepeats()
    {
        if (_repeats > 0)
        {
            WriteLineLocked($"{_clock.GetLocalNow():yyyy-MM-dd HH:mm:ss.fff} {"",-11} (the message above came {_repeats} more {(_repeats == 1 ? "time" : "times")})");
            _repeats = 0;
        }
    }

    private void WriteLineLocked(string line)
    {
        if (_writer is null)
        {
            return;
        }

        try
        {
            if (_writer.BaseStream.Length >= _maxBytes)
            {
                _writer.Dispose();
                _writer = Open();
            }

            _writer?.WriteLine(line);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A full disk or a log taken away; the app goes on without it.
            _writer?.Dispose();
            _writer = null;
        }
    }

    // Shared for reading, so the log can be opened in an editor while the app runs.
    private StreamWriter? Open()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            if (File.Exists(_path) && new FileInfo(_path).Length >= _maxBytes)
            {
                File.Move(_path, OldPath, overwrite: true);
            }

            var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            return new StreamWriter(stream) { AutoFlush = true };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
