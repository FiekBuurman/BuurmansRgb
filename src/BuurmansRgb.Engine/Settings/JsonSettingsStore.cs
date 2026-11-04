using System.Diagnostics;
using System.Text.Json;
using BuurmansRgb.Contracts;

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

    public static string DefaultFilePath { get; } = SettingsFiles.Default.PathOf(SettingsBackup.SettingsFileName);

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

    // Not as the store's own save: what it knows stays, so the file differs from it, as after a
    // change in an editor. The file reads (SettingsArchive), so a save must not move it aside.
    public void Replace(byte[] json)
    {
        lock (_gate)
        {
            JsonFile.Write(filePath, json);
            _unreadable = false;
        }
    }

    // A file that does not read is left where it is, unlike at the start: it may be half written
    // still, and the engine keeps running on what it has. Only a save moves it aside.
    public SettingsFileChange LoadIfChanged()
    {
        lock (_gate)
        {
            if (!File.Exists(filePath))
            {
                // The next save writes it again.
                _known = null;
                _unreadable = false;
                return SettingsFileChange.None;
            }

            byte[] json;
            try
            {
                json = File.ReadAllBytes(filePath);
            }
            catch (Exception exception) when (JsonFile.IsAccessError(exception))
            {
                // Held by the editor that is still writing it, say, or by a virus scanner that
                // looks at it: worth reading again a moment later.
                Trace.TraceWarning($"Settings file {filePath} changed but cannot be read now: {exception.Message}");
                return SettingsFileChange.NotReadableNow;
            }

            if (_known is not null && json.AsSpan().SequenceEqual(_known))
            {
                // Also a file that did not read and is now back as it was (undone in the editor):
                // the user's good file again, which a save must not move aside.
                _unreadable = false;
                return SettingsFileChange.None;
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
                return SettingsFileChange.None;
            }

            _known = json;
            _unreadable = false;
            return new SettingsFileChange(settings);
        }
    }

    private EngineSettings LoadOrCreateLocked()
    {
        if (!File.Exists(filePath))
        {
            // A new installation; written at once, so from the next start it is an existing file.
            var defaults = EngineSettings.CreateNew();
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
            // Run on the defaults meanwhile. The file goes aside first, where the user can fix a
            // typo and put it back, and the defaults are written at once: an engine that stops
            // before the next change must not find no file at the next start and take it for a
            // new installation (with its own defaults, such as "Uitzetten" during the pause).
            Trace.TraceError(exception.ToString());
            var unknown = UnknownChoices();
            if (TryMoveAside())
            {
                TrySaveLocked(unknown);
            }
            else
            {
                // Held by an editor without leave to move it, say: the file stays as it is, and the
                // next save tries to move it aside again rather than write over it.
                _unreadable = true;
            }

            return unknown;
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            // Out of reach for now; keep the user's file untouched and run on defaults meanwhile.
            return UnknownChoices();
        }
    }

    // The defaults for a file that is there but does not read: whether the user chose autostart
    // is not known then, so EngineHost reads it from the Run key, as for a file from before
    // autoStartChosen, and --first-run does not overrule a choice it cannot read.
    private static EngineSettings UnknownChoices() => EngineSettings.CreateDefault() with { AutoStartChosen = null };

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

    // A file that did not read when it last changed goes aside first, as at the start. If it cannot
    // go aside, nothing is written: the move throws, the file of the user stays, and the saver
    // tries again a few times (and logs it) before it gives up on this change.
    private void SaveLocked(byte[] json)
    {
        if (_unreadable && File.Exists(filePath))
        {
            File.Move(filePath, BrokenFilePath, overwrite: true);
        }

        _unreadable = false;

        JsonFile.Write(filePath, json);
        _known = json;
    }

    private bool TryMoveAside()
    {
        try
        {
            File.Move(filePath, BrokenFilePath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            Trace.TraceError(exception.ToString());
            return false;
        }
    }
}
