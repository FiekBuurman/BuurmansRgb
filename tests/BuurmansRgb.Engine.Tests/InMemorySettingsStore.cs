using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

// Thread-safe as JsonSettingsStore is: the saver and the reloader use it on their timer threads
// while a test changes it "by hand".
internal sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly Lock _gate = new();
    private EngineSettings _settings = EngineSettings.CreateDefault();
    private EngineSettings? _changedByHand;
    private Exception? _saveException;
    private int _saveCount;
    private int _loadIfChangedCount;
    private int _heldReads;

    public EngineSettings Settings
    {
        get
        {
            lock (_gate)
            {
                return _settings;
            }
        }
        set
        {
            lock (_gate)
            {
                _settings = value;
            }
        }
    }

    public Exception? SaveException
    {
        get
        {
            lock (_gate)
            {
                return _saveException;
            }
        }
        set
        {
            lock (_gate)
            {
                _saveException = value;
            }
        }
    }

    public int SaveCount
    {
        get
        {
            lock (_gate)
            {
                return _saveCount;
            }
        }
    }

    public int LoadIfChangedCount
    {
        get
        {
            lock (_gate)
            {
                return _loadIfChangedCount;
            }
        }
    }

    // How many LoadIfChanged calls in a row find the file held by another program.
    public int HeldReads
    {
        get
        {
            lock (_gate)
            {
                return _heldReads;
            }
        }
        set
        {
            lock (_gate)
            {
                _heldReads = value;
            }
        }
    }

    public EngineSettings LoadOrCreate() => Settings;

    // Settings changed "by hand": the next LoadIfChanged returns them, once. Until then
    // SaveIfUnchanged writes nothing.
    public void ChangeByHand(EngineSettings settings)
    {
        lock (_gate)
        {
            _settings = settings;
            _changedByHand = settings;
        }
    }

    public SettingsFileChange LoadIfChanged()
    {
        lock (_gate)
        {
            _loadIfChangedCount++;
            if (_heldReads > 0)
            {
                _heldReads--;
                return SettingsFileChange.NotReadableNow;
            }

            var settings = _changedByHand;
            _changedByHand = null;
            return new SettingsFileChange(settings);
        }
    }

    public void Save(EngineSettings settings)
    {
        lock (_gate)
        {
            SaveLocked(settings);
        }
    }

    public bool SaveIfUnchanged(EngineSettings settings)
    {
        lock (_gate)
        {
            if (_changedByHand is not null)
            {
                return false;
            }

            SaveLocked(settings);
            return true;
        }
    }

    private void SaveLocked(EngineSettings settings)
    {
        if (_saveException is not null)
        {
            throw _saveException;
        }

        _settings = settings;
        _changedByHand = null;
        _saveCount++;
    }
}
