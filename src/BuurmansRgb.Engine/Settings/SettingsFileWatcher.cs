using System.Diagnostics;

namespace BuurmansRgb.Engine.Settings;

// Watches settings.json with a FileSystemWatcher: Windows tells about a change, nothing polls. A
// directory that cannot be made or watched leaves the engine running without it, as before: a
// change by hand then waits for a restart.
internal sealed class SettingsFileWatcher : ISettingsFileEvents, IDisposable
{
    private readonly FileSystemWatcher? _watcher;

    public SettingsFileWatcher(string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
            Directory.CreateDirectory(directory);
            _watcher = new FileSystemWatcher(directory, Path.GetFileName(filePath))
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Renamed += OnChanged;
            _watcher.Error += OnError;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception) || exception is ArgumentException)
        {
            Trace.TraceWarning($"Changes to {filePath} are not picked up until a restart: {exception.Message}");
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    public event EventHandler? Changed;

    public void Dispose() => _watcher?.Dispose();

    // Also a rename of settings.json to another name, such as an editor that keeps a backup; the
    // store then finds the file gone or the same.
    private void OnChanged(object sender, FileSystemEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    // Too many changes at once for the watcher's buffer: one of them may have been settings.json.
    private void OnError(object sender, ErrorEventArgs e)
    {
        Trace.TraceWarning($"Watching the settings file: {e.GetException().Message}");
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
