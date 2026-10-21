using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Profiles;

// Loads a profile when its program starts, and goes back to the lighting from before once it
// stops, unless the lighting changed in the meantime (in the app, from the tray or at a time):
// then that stays. Two programs stack: when the last one stops, the one before it shows again.
// It looks for the programs every PollInterval, and only while a profile has a program; the
// process list holds no event for this without admin rights. A program that already runs when
// the engine starts loads its profile right away, with the saved lighting to go back to; one that
// already runs when its profile gets it counts from its next start. When the engine stops while a
// program runs, the lighting from before is put back as the program's stop would, so that is what
// the engine saves. Called from the tray thread, IPC threads and the timer; the lock guards the
// rest. Loading and restoring happen under the lock, so the LightingChanged they raise themselves
// is counted before the next change.
internal sealed class ProgramWatcher : IProfileTrigger
{
    public static TimeSpan DefaultPollInterval { get; } = TimeSpan.FromSeconds(2);

    private static readonly IReadOnlySet<string> NoPrograms = new HashSet<string>(ProgramNames.Comparer);

    private readonly ProfileLibrary _profiles;
    private readonly LightingController _lighting;
    private readonly IRunningPrograms _programs;
    private readonly TimeSpan _pollInterval;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();

    // The programs whose profile was loaded, in the order they started; the last one shows.
    private readonly List<Activation> _activations = [];

    // The profile to load by program; the first in alphabetical order when two have the same one.
    private Dictionary<string, string> _rules = new(ProgramNames.Comparer);
    private HashSet<string> _watched = new(ProgramNames.Comparer);
    private IReadOnlySet<string> _running = NoPrograms;

    // Counts LightingChanged, so a stop can tell whether anything changed since its profile loaded.
    private long _changes;
    private bool _started;
    private bool _disposed;

    public ProgramWatcher(ProfileLibrary profiles, LightingController lighting, IRunningPrograms programs, TimeProvider time, TimeSpan? pollInterval = null)
    {
        _profiles = profiles;
        _lighting = lighting;
        _programs = programs;
        _pollInterval = pollInterval ?? DefaultPollInterval;
        _timer = time.CreateTimer(_ => Poll(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    // Reads the profiles, so profiles.json is read at the start when it exists.
    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
        }

        _lighting.LightingChanged += OnLightingChanged;
        _profiles.Changed += OnProfilesChanged;
        UpdateRules(baseline: false);
        Poll();
    }

    public void Dispose()
    {
        _lighting.LightingChanged -= OnLightingChanged;
        _profiles.Changed -= OnProfilesChanged;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.Dispose();
            RestoreAll();
        }
    }

    private void OnLightingChanged(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            _changes++;
        }
    }

    private void OnProfilesChanged(object? sender, EventArgs e) => UpdateRules(baseline: true);

    // With baseline, what already runs of a newly watched program counts as running, not as started.
    private void UpdateRules(bool baseline)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var rules = new Dictionary<string, string>(ProgramNames.Comparer);
            foreach (var (name, automation) in _profiles.TryGetAutomated())
            {
                if (automation.Program is { } program)
                {
                    rules.TryAdd(program, name);
                }
            }

            var watched = new HashSet<string>(rules.Keys, ProgramNames.Comparer);

            // A program that is no longer watched is forgotten, without going back.
            for (var index = _activations.Count - 1; index >= 0; index--)
            {
                if (!watched.Contains(_activations[index].Program))
                {
                    Remove(index, restore: false);
                }
            }

            var added = new HashSet<string>(watched.Except(_watched, ProgramNames.Comparer), ProgramNames.Comparer);
            var running = new HashSet<string>(_running.Where(watched.Contains), ProgramNames.Comparer);
            if (baseline && added.Count > 0)
            {
                running.UnionWith(Find(added) ?? NoPrograms);
            }

            _rules = rules;
            _watched = watched;
            _running = running;
            _timer.Change(watched.Count > 0 ? _pollInterval : Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private void Poll()
    {
        lock (_gate)
        {
            if (_disposed || _watched.Count == 0)
            {
                return;
            }

            // When the list cannot be read, nothing started or stopped.
            if (Find(_watched) is { } running && !running.SetEquals(_running))
            {
                var stopped = _running.Where(program => !running.Contains(program)).ToList();
                var started = running.Where(program => !_running.Contains(program)).ToList();
                _running = running;
                stopped.ForEach(Stop);
                started.ForEach(StartProgram);
            }

            _timer.Change(_pollInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private void StartProgram(string program)
    {
        var coveredUntouched = _activations.Count == 0 || _activations[^1].ChangesAfter == _changes;
        var before = _lighting.State;
        var beforeScene = _profiles.ActiveName;
        try
        {
            _profiles.Load(_rules[program]);
        }
        catch (Exception exception)
        {
            // Deleted just now, or a device failed; the next start tries again.
            Trace.TraceError(exception.ToString());
            return;
        }

        _activations.Add(new Activation(program, before, beforeScene, _changes, coveredUntouched));
    }

    private void Stop(string program)
    {
        var index = _activations.FindIndex(activation => ProgramNames.Comparer.Equals(activation.Program, program));
        if (index >= 0)
        {
            Remove(index, restore: true);
        }
    }

    private void Remove(int index, bool restore)
    {
        var activation = _activations[index];
        _activations.RemoveAt(index);

        // Covered by a later program: that one goes back to where this one would have, as long as
        // this one's lighting was untouched when it was covered.
        if (index < _activations.Count)
        {
            var above = _activations[index];
            if (above.CoveredUntouched)
            {
                above.Before = activation.Before;
                above.BeforeScene = activation.BeforeScene;
                above.CoveredUntouched = activation.CoveredUntouched;
            }

            return;
        }

        if (!restore || activation.ChangesAfter != _changes || !Show(activation))
        {
            return;
        }

        // The program below shows its own lighting again, untouched if it was when covered.
        if (activation.CoveredUntouched && _activations.Count > 0)
        {
            _activations[^1].ChangesAfter = _changes;
        }
    }

    // While a program shows its profile untouched, the profile scene (such as one whose time came)
    // becomes what comes back once the programs stop, instead of showing now: 23:00 in the middle
    // of a game. False when no program shows its own profile; then the caller shows it. A profile
    // that is gone throws ArgumentException, as loading it would.
    public bool HoldUntilProgramsStop(string scene)
    {
        lock (_gate)
        {
            if (_disposed || RestoreIndexLocked() is not { } index)
            {
                return false;
            }

            _activations[index].Before = _profiles.GetLighting(scene);
            _activations[index].BeforeScene = scene;
            return true;
        }
    }

    // Where stopping every program from the last one down would end, so what the engine saves.
    private void RestoreAll()
    {
        if (RestoreIndexLocked() is { } index)
        {
            Show(_activations[index]);
        }

        _activations.Clear();
    }

    // The program whose lighting from before comes back once all programs stopped: the first
    // untouched one going down from the last; null when the last one was touched.
    private int? RestoreIndexLocked()
    {
        if (_activations.Count == 0 || _activations[^1].ChangesAfter != _changes)
        {
            return null;
        }

        var index = _activations.Count - 1;
        while (index > 0 && _activations[index].CoveredUntouched)
        {
            index--;
        }

        return index;
    }

    // The lighting from before, with the scene that was on then, so its card is the active one again.
    private bool Show(Activation activation)
    {
        try
        {
            _profiles.Show(activation.Before, activation.BeforeScene);
            return true;
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
            return false;
        }
    }

    private IReadOnlySet<string>? Find(IReadOnlySet<string> programs)
    {
        try
        {
            return _programs.Find(programs);
        }
        catch (Exception exception)
        {
            Trace.TraceError(exception.ToString());
            return null;
        }
    }

    // ChangesAfter is the change count right after its profile loaded; CoveredUntouched tells
    // whether the program below still showed its own lighting when this one started. BeforeScene is
    // the scene that was on with Before, or null.
    private sealed class Activation(string program, LightingState before, string? beforeScene, long changesAfter, bool coveredUntouched)
    {
        public string Program { get; } = program;

        public LightingState Before { get; set; } = before;

        public string? BeforeScene { get; set; } = beforeScene;

        public long ChangesAfter { get; set; } = changesAfter;

        public bool CoveredUntouched { get; set; } = coveredUntouched;
    }
}
