namespace BuurmansRgb.Engine.Profiles;

// Which scene is on, for ProfileLibrary.ActiveName: the one loaded or saved last, until the lighting
// changes in any other way. Its own lock, apart from the one of the library, because LightingChanged
// comes from inside the lighting controller, also while another thread holds the lock of the
// library. Nothing outside is called under this lock, so the library may read Name under its own
// lock (Load), never the other way round. Changed goes out outside the lock, from Change only.
internal sealed class ActiveSceneTracker
{
    private readonly Lock _gate = new();
    private string? _name;

    // The scene that Showing is putting on, and its thread: the LightingChanged of that thread is the
    // scene coming on, any other is a change that ends it.
    private string? _showingName;
    private int _showingThread;

    // Raised when another scene is on, or none. Outside the lock.
    public event EventHandler? Changed;

    // The name of the scene that is on, in its saved spelling; null when none is.
    public string? Name
    {
        get
        {
            lock (_gate)
            {
                return _name;
            }
        }
    }

    // Without Changed: the scene that was on when the engine stopped (ProfileLibrary.RestoreActive).
    public void Restore(string? name)
    {
        lock (_gate)
        {
            _name = name;
        }
    }

    public void Set(string? name) => Change(_ => name);

    // No scene on when gone says so of the scene that is on: it was deleted, or is not in a file
    // that replaced profiles.json.
    public void EndIf(Func<string, bool> gone) => Change(active => active is not null && gone(active) ? null : active);

    // The lighting changed: what Showing puts on on this thread is that scene, anything else ends it.
    public void OnLightingChanged() => Change(_ => _showingThread == Environment.CurrentManagedThreadId ? _showingName : null);

    // Runs replace, which changes the lighting, with scene as what that puts on (OnLightingChanged).
    public void Showing(string? scene, Action replace)
    {
        lock (_gate)
        {
            _showingName = scene;
            _showingThread = Environment.CurrentManagedThreadId;
        }

        try
        {
            replace();
        }
        finally
        {
            lock (_gate)
            {
                _showingName = null;
                _showingThread = 0;
            }
        }
    }

    // The scene that is on becomes what next makes of it, under the lock; Changed follows outside
    // the lock when that is another scene than before.
    private void Change(Func<string?, string?> next)
    {
        bool changed;
        lock (_gate)
        {
            var name = next(_name);
            changed = !string.Equals(_name, name, StringComparison.Ordinal);
            _name = name;
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
