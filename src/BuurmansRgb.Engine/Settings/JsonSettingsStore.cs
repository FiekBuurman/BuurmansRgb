using System.Diagnostics;
using System.Text.Json;

namespace BuurmansRgb.Engine.Settings;

// Thread-safe: the saver writes on its timer thread and SettingsReloader reads on its own.
internal sealed class JsonSettingsStore(string filePath) : ISettingsStore
{
    private readonly Lock _gate = new();

    // What the file held when this store last read or wrote it, to tell a change by someone else
    // from its own save. The content, not a time: a save raises several file events, which may come
    // in long after it.
    private byte[]? _known;

    // Whether the file did not read when it last changed: the next save moves it aside first.
    private bool _unreadable;

    public static string DefaultFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BuurmansRgb",
        "settings.json");

    // Where a file that does not read goes, so the next save does not overwrite the user's settings.
    public string BrokenFilePath => filePath + ".broken";

    public EngineSettings LoadOrCreate()
    {
        lock (_gate)
        {
            return LoadOrCreateLocked();
        }
    }

    public void Save(EngineSettings settings)
    {
        var json = settings.ToJson();
        lock (_gate)
        {
            SaveLocked(json);
        }
    }

    // The user may still save the file between this reading it and moving the save over it; that
    // window is short, as the save is a move.
    public bool SaveIfUnchanged(EngineSettings settings)
    {
        var json = settings.ToJson();
        lock (_gate)
        {
            if (!_unreadable && File.Exists(filePath) && !HoldsKnownLocked())
            {
                Trace.TraceInformation($"Settings file {filePath} changed by someone else; it goes before the engine's save.");
                return false;
            }

            SaveLocked(json);
            return true;
        }
    }

    // A file that does not read is left where it is, unlike at the start: it may be half written
    // still, and the engine keeps running on what it has. Only a save moves it aside.
    public EngineSettings? LoadIfChanged()
    {
        lock (_gate)
        {
            if (!File.Exists(filePath))
            {
                // The next save writes it again.
                _known = null;
                _unreadable = false;
                return null;
            }

            byte[] json;
            try
            {
                json = File.ReadAllBytes(filePath);
            }
            catch (Exception exception) when (JsonFile.IsAccessError(exception))
            {
                // Held by the editor that is still writing it, say; its last write comes as a change too.
                Trace.TraceWarning($"Settings file {filePath} changed but cannot be read now: {exception.Message}");
                return null;
            }

            if (_known is not null && json.AsSpan().SequenceEqual(_known))
            {
                // Also a file that did not read and is now back as it was (undone in the editor):
                // the user's good file again, which a save must not move aside.
                _unreadable = false;
                return null;
            }

            EngineSettings settings;
            try
            {
                settings = LenientJson.Deserialize(json, EngineSettingsJsonContext.Default.EngineSettings) ?? EngineSettings.CreateDefault();
            }
            catch (JsonException exception)
            {
                Trace.TraceWarning($"Settings file {filePath} changed but does not read; nothing changes until it does: {exception.Message}");
                _unreadable = true;
                return null;
            }

            _known = json;
            _unreadable = false;
            return settings;
        }
    }

    private EngineSettings LoadOrCreateLocked()
    {
        if (!File.Exists(filePath))
        {
            var defaults = EngineSettings.CreateDefault();
            TrySaveLocked(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllBytes(filePath);
            var settings = LenientJson.Deserialize(json, EngineSettingsJsonContext.Default.EngineSettings) ?? EngineSettings.CreateDefault();
            _known = json;
            return settings;
        }
        catch (JsonException exception)
        {
            // Run on the defaults meanwhile. The engine saves them a moment after the next change,
            // possibly without the user doing anything (a profile that loads by itself), so the
            // file goes aside first, where the user can fix a typo and put it back.
            Trace.TraceError(exception.ToString());
            TryMoveAside();
            return EngineSettings.CreateDefault();
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            // Out of reach for now; keep the user's file untouched and run on defaults meanwhile.
            return EngineSettings.CreateDefault();
        }
    }

    // Writing the defaults is a convenience for the user; the lights must start without it.
    private void TrySaveLocked(EngineSettings settings)
    {
        try
        {
            SaveLocked(settings.ToJson());
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
        }
    }

    private bool HoldsKnownLocked() =>
        _known is not null && File.ReadAllBytes(filePath).AsSpan().SequenceEqual(_known);

    // A file that did not read when it last changed goes aside first, as at the start.
    private void SaveLocked(byte[] json)
    {
        if (_unreadable)
        {
            TryMoveAside();
            _unreadable = false;
        }

        JsonFile.Write(filePath, json);
        _known = json;
    }

    private void TryMoveAside()
    {
        try
        {
            File.Move(filePath, BrokenFilePath, overwrite: true);
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            Trace.TraceError(exception.ToString());
        }
    }
}
