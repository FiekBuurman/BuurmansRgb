using System.Diagnostics;
using BuurmansRgb.Contracts;
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
// ArgumentException, which reaches the UI as an RPC error.
internal sealed class CustomEffectLibrary : IDisposable
{
    private readonly ICustomEffectStore _store;
    private readonly LightingController _lighting;
    private readonly TimeSpan _saveDelay;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();
    private List<CustomEffect>? _effects;

    // Whether the list in memory still has to go to the file: from a change until a write that
    // went through, so after a failed one the next change or the exit tries again.
    private bool _pending;
    private bool _disposed;

    public CustomEffectLibrary(ICustomEffectStore store, LightingController lighting, TimeSpan? saveDelay = null, TimeProvider? timeProvider = null)
    {
        _store = store;
        _lighting = lighting;
        _saveDelay = saveDelay ?? DefaultSaveDelay;
        _timer = (timeProvider ?? TimeProvider.System).CreateTimer(
            _ => SavePending(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    // Raised after the list changed: an effect was saved or deleted. Outside the lock, so a
    // listener can read the library again.
    public event EventHandler? Changed;

    public static TimeSpan DefaultSaveDelay { get; } = TimeSpan.FromSeconds(1);

    // In alphabetical order of their names.
    public IReadOnlyList<CustomEffect> Effects
    {
        get
        {
            lock (_gate)
            {
                return [.. EffectsLocked()];
            }
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
        SavePending();
    }

    // Reads the file and gives the effects to the lighting. A file that cannot be read now, such as
    // one another program holds, gives the lighting nothing: until the next question reads it
    // again, a device keeps the own effect it shows and only shows its color.
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
            }
        }
    }

    // Saves a new effect, with an empty id, under a new id, or replaces the effect with its id,
    // and returns it as it is kept (CustomEffects.Normalize). The name must differ from that of the
    // other effects, and there are at most CustomEffects.MaxEffects.
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
            if (effects.Exists(other => other.Id != saved.Id && CustomEffects.NameComparer.Equals(other.Name, saved.Name)))
            {
                throw new ArgumentException($"There is already an own effect named '{saved.Name}'.", nameof(effect));
            }

            Store([.. effects.Where(other => other.Id != saved.Id), saved]);
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
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Reading the file throws when it is out of reach; the list stays unread, so a change is never
    // written over effects that were not read.
    private List<CustomEffect> EffectsLocked()
    {
        if (_effects is null)
        {
            _effects = Sorted(_store.Load());
            _lighting.SetCustomEffects(_effects);
        }

        return _effects;
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

    // A file that cannot be written now keeps the change pending: the lights show it, and the next
    // change or the exit writes it.
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
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            Trace.TraceError(exception.ToString());
        }
    }

    private static List<CustomEffect> Sorted(IEnumerable<CustomEffect> effects) => [.. effects.OrderBy(effect => effect.Name, CustomEffects.NameComparer)];
}
