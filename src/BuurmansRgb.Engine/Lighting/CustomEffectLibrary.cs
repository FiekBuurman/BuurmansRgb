using System.Diagnostics;
using System.Globalization;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Resources;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Lighting;

// The own effects of the user, all of them kept in memory once read (a step with a color per LED
// for 104 LEDs is about 1 KB in the file), in alphabetical order of their names. Read at the start
// (Load), before the lighting is applied, so the lighting keeps an own effect it shows. A change
// counts at once, in memory and on the lighting; the file is written a moment after the last
// change (as LightingSettingsSaver does), so dragging a slider in the editor writes it once, and at
// the exit (Dispose). LightingController gets every new list, so a device that shows an effect
// shows its new steps at once, and one that shows an effect that was deleted gets static. Called
// from IPC threads; the lock guards the list. An invalid effect or an unknown id throws
// ArgumentException, which reaches the UI as an RPC error. A file that cannot be read at the start
// is read again every LoadRetryDelay until that works, also without a settings app that asks.
internal sealed class CustomEffectLibrary : IDisposable
{
    private readonly ICustomEffectStore _store;
    private readonly LightingController _lighting;
    private readonly TimeSpan _saveDelay;
    private readonly ITimer _timer;
    private readonly ITimer _loadTimer;
    private readonly Lock _gate = new();
    private List<CustomEffect>? _effects;

    // Whether the list in memory still has to go to the file: from a change until a write that
    // went through, so after a failed one the next change or the exit tries again.
    private bool _pending;
    private bool _disposed;

    // From a Load that could not read the file until a read that went through; that read raises
    // Changed, so a settings app that got no effects asks again.
    private bool _loadFailed;

    // From a failed write until one that went through, so only the first failure is logged whole.
    private bool _saveFailed;

    public CustomEffectLibrary(ICustomEffectStore store, LightingController lighting, TimeSpan? saveDelay = null, TimeProvider? timeProvider = null)
    {
        _store = store;
        _lighting = lighting;
        _saveDelay = saveDelay ?? DefaultSaveDelay;
        var time = timeProvider ?? TimeProvider.System;
        _timer = time.CreateTimer(_ => SavePending(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _loadTimer = time.CreateTimer(_ => LoadAgain(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    // Raised after the list changed: an effect was saved or deleted. Outside the lock, so a
    // listener can read the library again.
    public event EventHandler? Changed;

    public static TimeSpan DefaultSaveDelay { get; } = TimeSpan.FromSeconds(1);

    // How often a file that could not be read at the start is tried again.
    public static TimeSpan LoadRetryDelay { get; } = TimeSpan.FromSeconds(5);

    // In alphabetical order of their names. The first read after a failed Load raises Changed.
    public IReadOnlyList<CustomEffect> Effects
    {
        get
        {
            List<CustomEffect> effects;
            bool loaded;
            lock (_gate)
            {
                effects = [.. EffectsLocked()];
                loaded = TakeLoadedAfterFailure();
            }

            if (loaded)
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }

            return effects;
        }
    }

    // Writes a change that waits right away, so nothing is lost when the engine exits; a change
    // after this is written at once. A timer callback that is still running holds the lock, so
    // this waits for it.
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _timer.Dispose();
        _loadTimer.Dispose();
        SavePending();
    }

    // Reads the file and gives the effects to the lighting. A file that cannot be read now, such as
    // one another program holds, gives the lighting nothing: until the retry timer or a question
    // reads it, a device keeps the own effect it shows and only shows its color.
    public void Load()
    {
        lock (_gate)
        {
            try
            {
                EffectsLocked();
            }
            catch (Exception exception) when (JsonFile.IsAccessError(exception))
            {
                Trace.TraceError(exception.ToString());
                if (!_disposed)
                {
                    _loadFailed = true;
                    _loadTimer.Change(LoadRetryDelay, LoadRetryDelay);
                }
            }
        }
    }

    // Saves a new effect, with an empty id, under a new id, or replaces the effect with its id,
    // and returns it as it is kept (CustomEffects.Normalize). The name must differ from that of the
    // other effects and from those of the effects of the engine (EffectNames), and there are at
    // most CustomEffects.MaxEffects.
    public CustomEffect Save(CustomEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        CustomEffect saved;
        lock (_gate)
        {
            var effects = EffectsLocked();
            var isNew = string.IsNullOrEmpty(effect.Id);
            if (isNew)
            {
                if (effects.Count >= CustomEffects.MaxEffects)
                {
                    throw new ArgumentException($"There are at most {CustomEffects.MaxEffects} own effects.", nameof(effect));
                }

                var id = CustomEffects.NewId();
                while (effects.Exists(other => other.Id == id))
                {
                    id = CustomEffects.NewId();
                }

                effect = effect with { Id = id };
            }
            else if (!effects.Exists(other => other.Id == effect.Id))
            {
                throw new ArgumentException($"There is no own effect '{effect.Id}'.", nameof(effect));
            }

            saved = CustomEffects.Normalize(effect);
            if (EffectNames.IsBuiltIn(saved.Name))
            {
                throw new ArgumentException($"'{saved.Name}' is the name of an effect of the engine.", nameof(effect));
            }

            if (effects.Exists(other => other.Id != saved.Id && CustomEffects.NameComparer.Equals(other.Name, saved.Name)))
            {
                throw new ArgumentException($"There is already an own effect named '{saved.Name}'.", nameof(effect));
            }

            Store([.. effects.Where(other => other.Id != saved.Id), saved]);

            // Changed below also tells of a list read after a failed Load.
            TakeLoadedAfterFailure();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return saved;
    }

    public void Delete(string id)
    {
        lock (_gate)
        {
            var effects = EffectsLocked();
            if (!effects.Exists(effect => effect.Id == id))
            {
                throw new ArgumentException($"There is no own effect '{id}'.", nameof(id));
            }

            Store([.. effects.Where(effect => effect.Id != id)]);
            TakeLoadedAfterFailure();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Reading the file throws when it is out of reach; the list stays unread, so a change is never
    // written over effects that were not read.
    private List<CustomEffect> EffectsLocked()
    {
        if (_effects is null)
        {
            _effects = Sorted(WithoutBuiltInNames(_store.Load()));
            _lighting.SetCustomEffects(_effects);
        }

        return _effects;
    }

    // Whether the list was read after a failed Load and the apps were not told yet; stops the
    // retry timer then. After Dispose that timer is gone already, and changing it may throw.
    private bool TakeLoadedAfterFailure()
    {
        if (!_loadFailed || _effects is null)
        {
            return false;
        }

        _loadFailed = false;
        if (!_disposed)
        {
            _loadTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        return true;
    }

    // The retry timer: reads the file when no question did meanwhile, and tells the apps.
    private void LoadAgain()
    {
        bool loaded;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                EffectsLocked();
            }
            catch (Exception exception) when (JsonFile.IsAccessError(exception))
            {
                // Load logged the whole first failure; every few seconds the message is enough.
                Trace.TraceWarning($"Still could not read the own effects: {exception.Message}");
                return;
            }

            loaded = TakeLoadedAfterFailure();
            if (loaded)
            {
                Trace.TraceInformation("Read the own effects after all.");
            }
        }

        if (loaded)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    // The lighting gets the list under the lock, so two changes at once reach it in the order they
    // are kept here. Its LightingChanged, for a device that showed a deleted effect, then runs
    // under this lock too; that is safe, as no listener (the settings saver, the scenes, the IPC
    // connections) calls this library, and the lock of the lighting is not held then.
    private void Store(List<CustomEffect> effects)
    {
        var sorted = Sorted(effects);
        _effects = sorted;
        _lighting.SetCustomEffects(sorted);
        _pending = true;
        if (_disposed)
        {
            SavePendingLocked();
        }
        else
        {
            _timer.Change(_saveDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void SavePending()
    {
        lock (_gate)
        {
            SavePendingLocked();
        }
    }

    // A file that cannot be written now keeps the change pending: the lights show it, and the timer
    // tries again a moment later, so a crash does not cost all changes since; the next change or
    // the exit writes it too.
    private void SavePendingLocked()
    {
        if (!_pending || _effects is null)
        {
            return;
        }

        try
        {
            _store.Save(_effects);
            _pending = false;
            if (_saveFailed)
            {
                _saveFailed = false;
                Trace.TraceInformation("Wrote the own effects after all.");
            }
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            // The whole first failure; a timer that tries again every moment only adds its message.
            if (_saveFailed)
            {
                Trace.TraceWarning($"Still could not write the own effects: {exception.Message}");
            }
            else
            {
                Trace.TraceError(exception.ToString());
                _saveFailed = true;
            }

            if (!_disposed)
            {
                _timer.Change(_saveDelay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    // An effect that was saved under the name of an effect of the engine before the engine refused
    // that, such as "Regenboog", is not lost but gets "Regenboog (eigen)", or with a number when
    // that is taken too; the file gets the new name with the next change.
    private static List<CustomEffect> WithoutBuiltInNames(IReadOnlyList<CustomEffect> effects)
    {
        var renamed = effects.ToList();
        for (var i = 0; i < renamed.Count; i++)
        {
            var effect = renamed[i];
            if (!EffectNames.IsBuiltIn(effect.Name))
            {
                continue;
            }

            var own = string.Format(CultureInfo.CurrentCulture, Strings.CustomEffectOwnName, effect.Name);
            var name = CustomEffects.FirstFreeName(
                number => number == 1 ? own : string.Create(CultureInfo.CurrentCulture, $"{own} {number}"),
                candidate => EffectNames.IsBuiltIn(candidate) || renamed.Exists(other => CustomEffects.NameComparer.Equals(other.Name, candidate)));
            Trace.TraceWarning($"The own effect '{effect.Name}' has the name of an effect of the engine; it is called '{name}' now.");
            renamed[i] = effect with { Name = name };
        }

        return renamed;
    }

    private static List<CustomEffect> Sorted(IEnumerable<CustomEffect> effects) => [.. effects.OrderBy(effect => effect.Name, CustomEffects.NameComparer)];
}
