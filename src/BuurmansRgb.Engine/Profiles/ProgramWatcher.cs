using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
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
// is counted before the next change. While Windows is locked or asleep (IAwayState), it does
// not look, also when the effects keep running then: nobody starts a game on the lock screen.
// Right after, it looks once at once, so what started or stopped in the meantime counts then;
// a time that comes meanwhile looks once too (HoldUntilProgramsStop).
//
// A program whose profile says ForegroundOnly counts only while its window is in the foreground
// (IForegroundWindow), as if it started when it comes to the front and stopped when another program
// does; that needs no polling, and it is followed only while some profile asks for it. Going from
// one such program to another loads the second on top of the first, which then goes from under it,
// so no lighting shows in between. While one is in front, it is also looked for with the others,
// so it counts as stopped when it closes without another window coming to the front. While
// resting the foreground does not count either; right after, what is in front then counts.
internal sealed class ProgramWatcher : IProfileTrigger
{
    public static TimeSpan DefaultPollInterval { get; } = TimeSpan.FromSeconds(2);

    private static readonly IReadOnlySet<string> NoPrograms = new HashSet<string>(ProgramNames.Comparer);

    private readonly ProfileLibrary _profiles;
    private readonly LightingController _lighting;
    private readonly IRunningPrograms _programs;
    private readonly IAwayState _away;
    private readonly IForegroundWindow _foreground;
    private readonly TimeSpan _pollInterval;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();

    // The programs whose profile was loaded, in the order they started; the last one shows.
    private readonly List<Activation> _activations = [];

    // The profile to load by program; the first in alphabetical order when two have the same one.
    private Dictionary<string, Rule> _rules = new(ProgramNames.Comparer);

    // The programs that count while they run, and of those the ones that run.
    private HashSet<string> _watched = new(ProgramNames.Comparer);
    private IReadOnlySet<string> _running = NoPrograms;

    // The program that counts while in the foreground and is in front, or null; and whether a look
    // saw it run, so a name that cannot be read yet does not count as stopped.
    private string? _inFront;
    private bool _inFrontSeen;

    // The program of the window that came to the front last while followed, of any kind, or null;
    // also while resting, so right after that counts. Forgotten when a look sees it stopped.
    private string? _front;

    // What a look looks for: _watched, and _inFront.
    private HashSet<string> _polled = new(ProgramNames.Comparer);

    // Whether the foreground is followed, and whether the next Poll checks what is in front first.
    private bool _following;
    private bool _checkFront;

    // Counts LightingChanged, so a stop can tell whether anything changed since its profile loaded.
    private long _changes;
    private bool _resting;
    private bool _started;
    private bool _disposed;

    public ProgramWatcher(ProfileLibrary profiles, LightingController lighting, IRunningPrograms programs, IAwayState away, IForegroundWindow foreground, TimeProvider time, TimeSpan? pollInterval = null)
    {
        _profiles = profiles;
        _lighting = lighting;
        _programs = programs;
        _away = away;
        _foreground = foreground;
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
        _foreground.Changed += OnForegroundChanged;
        UpdateRules(baseline: false);
        _away.AwayChanged += OnAwayChanged;
        OnAwayChanged(this, _away.Away);
        Poll();
    }

    public void Dispose()
    {
        _lighting.LightingChanged -= OnLightingChanged;
        _profiles.Changed -= OnProfilesChanged;
        _foreground.Changed -= OnForegroundChanged;
        _away.AwayChanged -= OnAwayChanged;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer.Dispose();
            FollowLocked(false);
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

    // Raised under the lock of AutoPause, and nothing here asks it anything while holding _gate.
    private void OnAwayChanged(object? sender, PauseReasons away)
    {
        lock (_gate)
        {
            var resting = away != PauseReasons.None;
            if (_disposed || resting == _resting)
            {
                return;
            }

            _resting = resting;

            // On the timer, like the look, so no profile loads under the lock of AutoPause.
            _checkFront = !resting && _following;
            ScheduleLocked(resting ? Timeout.InfiniteTimeSpan : TimeSpan.Zero);
        }
    }

    // Raised on the tray thread.
    private void OnForegroundChanged(object? sender, string? program)
    {
        lock (_gate)
        {
            if (_disposed || !_following)
            {
                return;
            }

            _front = program;
            if (!_resting)
            {
                ShowInFrontLocked(program);
            }
        }
    }

    // Only while there is something to look for or the foreground is to be checked, and not while
    // resting.
    private void ScheduleLocked(TimeSpan due) =>
        _timer.Change((_polled.Count > 0 || _checkFront) && !_resting ? due : Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    // With baseline, what already runs of a newly watched program counts as running, not as started.
    // A program in front counts only from the next time it comes to the front.
    private void UpdateRules(bool baseline)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var rules = new Dictionary<string, Rule>(ProgramNames.Comparer);
            foreach (var (name, automation) in _profiles.TryGetAutomated())
            {
                if (automation.Program is { } program)
                {
                    rules.TryAdd(program, new Rule(name, automation.ForegroundOnly));
                }
            }

            var watched = new HashSet<string>(rules.Where(rule => !rule.Value.ForegroundOnly).Select(rule => rule.Key), ProgramNames.Comparer);

            // A program that is no longer watched, or no longer in the same way, is forgotten,
            // without going back.
            for (var index = _activations.Count - 1; index >= 0; index--)
            {
                var program = _activations[index].Program;
                if (!rules.TryGetValue(program, out var rule) || rule.ForegroundOnly != _rules[program].ForegroundOnly)
                {
                    Remove(index, restore: false);
                }
            }

            if (_inFront is { } front && !(rules.TryGetValue(front, out var frontRule) && frontRule.ForegroundOnly))
            {
                _inFront = null;
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
            UpdatePolledLocked();
            FollowLocked(rules.Values.Any(rule => rule.ForegroundOnly));
            ScheduleLocked(_pollInterval);
        }
    }

    private void FollowLocked(bool on)
    {
        if (on != _following)
        {
            _following = on;
            _front = null;
            _foreground.Follow(on);
        }
    }

    private void UpdatePolledLocked()
    {
        var polled = new HashSet<string>(_watched, ProgramNames.Comparer);
        if (_inFront is { } front)
        {
            polled.Add(front);
        }

        _polled = polled;
    }

    private void Poll()
    {
        lock (_gate)
        {
            if (_disposed || _resting)
            {
                return;
            }

            if (_checkFront)
            {
                _checkFront = false;
                if (_following)
                {
                    ShowInFrontLocked(_front);
                }
            }

            if (_polled.Count == 0)
            {
                return;
            }

            LookLocked();
            ScheduleLocked(_pollInterval);
        }
    }

    // When the list cannot be read, nothing started or stopped.
    private void LookLocked()
    {
        if (Find(_polled) is not { } found)
        {
            return;
        }

        var running = found;
        if (_inFront is { } front)
        {
            if (found.Contains(front))
            {
                _inFrontSeen = true;
            }
            else if (_inFrontSeen)
            {
                // Closed, and no window of another program came to the front since. When it comes
                // to the front again, Windows tells that as for any window.
                if (ProgramNames.Comparer.Equals(_front, front))
                {
                    _front = null;
                }

                ShowInFrontLocked(null);
            }

            running = new HashSet<string>(found.Where(_watched.Contains), ProgramNames.Comparer);
        }

        if (!running.SetEquals(_running))
        {
            var stopped = _running.Where(program => !running.Contains(program)).ToList();
            var started = running.Where(program => !_running.Contains(program)).ToList();
            _running = running;
            stopped.ForEach(Stop);
            started.ForEach(StartProgram);
        }
    }

    // program is the one whose window is in front now, of any kind. Only one that counts in the
    // foreground loads its profile; the one that was in front before stops after it, so from one
    // to the other no lighting shows in between.
    private void ShowInFrontLocked(string? program)
    {
        var front = program is not null && _rules.TryGetValue(program, out var rule) && rule.ForegroundOnly ? program : null;
        if (ProgramNames.Comparer.Equals(front, _inFront))
        {
            return;
        }

        var behind = _inFront;
        var polling = _polled.Count > 0;
        _inFront = front;
        _inFrontSeen = false;
        UpdatePolledLocked();
        if (!polling)
        {
            ScheduleLocked(_pollInterval);
        }

        if (front is not null)
        {
            StartProgram(front);
        }

        if (behind is not null)
        {
            Stop(behind);
        }
    }

    private void StartProgram(string program)
    {
        var coveredUntouched = _activations.Count == 0 || _activations[^1].ChangesAfter == _changes;
        var before = _lighting.State;
        var beforeScene = _profiles.ActiveName;
        try
        {
            _profiles.Load(_rules[program].Profile);
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
    // that is gone throws ArgumentException, as loading it would. While resting it looks once
    // first, so a game that stopped on the lock screen does not hold the time until unlocking.
    public bool HoldUntilProgramsStop(string scene)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            if (_resting && _activations.Count > 0)
            {
                LookLocked();
            }

            if (RestoreIndexLocked() is not { } index)
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

    // The profile a program loads, and whether only while it is in the foreground.
    private readonly record struct Rule(string Profile, bool ForegroundOnly);

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
