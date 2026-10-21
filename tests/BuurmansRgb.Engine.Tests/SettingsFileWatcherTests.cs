using System.Collections.Concurrent;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

// With a real FileSystemWatcher, in a directory of its own.
public sealed class SettingsFileWatcherTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private string FilePath => _directory.File("settings.json");

    [Fact]
    public async Task FileWrittenByHand_RaisesChanged()
    {
        using var watcher = new SettingsFileWatcher(FilePath);
        var changes = 0;
        watcher.Changed += (_, _) => Interlocked.Increment(ref changes);

        await File.WriteAllTextAsync(FilePath, """{ "framesPerSecond": 12 }""", Ct);

        await Eventually.TrueAsync(() => Volatile.Read(ref changes) > 0);
    }

    // How the engine saves, and some editors: to another file that is then moved over it.
    [Fact]
    public async Task FileMovedOverIt_RaisesChanged()
    {
        await File.WriteAllTextAsync(FilePath, "{}", Ct);
        using var watcher = new SettingsFileWatcher(FilePath);
        var changes = 0;
        watcher.Changed += (_, _) => Interlocked.Increment(ref changes);

        JsonFile.Write(FilePath, """{ "framesPerSecond": 12 }"""u8.ToArray());

        await Eventually.TrueAsync(() => Volatile.Read(ref changes) > 0);
    }

    [Fact]
    public async Task OtherFileInTheDirectory_RaisesNothing()
    {
        using var watcher = new SettingsFileWatcher(FilePath);
        var changes = 0;
        watcher.Changed += (_, _) => Interlocked.Increment(ref changes);

        await File.WriteAllTextAsync(_directory.File("profiles.json"), "{}", Ct);
        await File.WriteAllTextAsync(FilePath, "{}", Ct);

        // The change of settings.json comes after the one of profiles.json, so once it is in the
        // other had its chance.
        await Eventually.TrueAsync(() => Volatile.Read(ref changes) > 0);
        var afterSettings = Volatile.Read(ref changes);
        await File.WriteAllTextAsync(_directory.File("profiles.json"), """{ "profiles": [] }""", Ct);
        await Task.Delay(TimeSpan.FromMilliseconds(300), Ct);
        Assert.Equal(afterSettings, Volatile.Read(ref changes));
    }

    // A directory that cannot be made: the engine runs on without picking up changes.
    [Fact]
    public void DirectoryThatCannotBeMade_WatchesNothing()
    {
        var blocker = _directory.File("blocker");
        File.WriteAllText(blocker, "a file where the settings directory should be");

        var path = Path.Combine(blocker, "settings.json");
        using var trace = new TraceCapture();

        using var watcher = new SettingsFileWatcher(path);

        Assert.Contains(trace.Messages, message => message.Contains(path, StringComparison.Ordinal));
    }

    // The whole chain: a change by hand is applied within a second, the store's own save is not.
    [Fact]
    public async Task WithTheStoreAndTheReloader_AppliesAChangeByHandAndNotItsOwnSave()
    {
        var store = new JsonSettingsStore(FilePath);
        store.LoadOrCreate();
        using var watcher = new SettingsFileWatcher(FilePath);
        var applied = new ConcurrentQueue<EngineSettings>();
        using var reloader = new SettingsReloader(watcher, store, applied.Enqueue, SettingsReloader.DefaultDelay);

        await File.WriteAllTextAsync(FilePath, """{ "framesPerSecond": 12 }""", Ct);
        await Eventually.TrueAsync(() => !applied.IsEmpty);
        Assert.Equal(12, Assert.Single(applied).FramesPerSecond);

        store.Save(new EngineSettings { FramesPerSecond = 24 });
        await Task.Delay(SettingsReloader.DefaultDelay * 3, Ct);

        Assert.Single(applied);
    }
}
