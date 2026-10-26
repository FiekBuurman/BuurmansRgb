using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Lighting;

// The own effects of the user, all of them kept in memory once read (a step with a color per LED
// for 104 LEDs is about 1 KB in the file), in alphabetical order of their names. Read at the start
// (Load), before the lighting is applied, so the lighting keeps an own effect it shows; written on
// every change. LightingController gets every new list, so a device that shows an effect shows its
// new steps at once, and one that shows an effect that was deleted gets static. Called from IPC
// threads; the lock guards the list. An invalid effect or an unknown id throws ArgumentException,
// which reaches the UI as an RPC error.
internal sealed class CustomEffectLibrary(ICustomEffectStore store, LightingController lighting)
{
    private readonly Lock _gate = new();
    private List<CustomEffect>? _effects;

    // Raised after the list was written: an effect was saved or deleted. Outside the lock, so a
    // listener can read the library again.
    public event EventHandler? Changed;

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

    // Reads the file and gives the effects to the lighting. A file that cannot be read now, such as
    // one another program holds, gives none for the moment; the next question reads it again.
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
                lighting.SetCustomEffects([]);
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

    private List<CustomEffect> EffectsLocked()
    {
        if (_effects is null)
        {
            _effects = Sorted(store.Load());
            lighting.SetCustomEffects(_effects);
        }

        return _effects;
    }

    // The file first, so the effects only change once they are saved.
    private void Store(List<CustomEffect> effects)
    {
        var sorted = Sorted(effects);
        store.Save(sorted);
        _effects = sorted;
        lighting.SetCustomEffects(sorted);
    }

    private static List<CustomEffect> Sorted(IEnumerable<CustomEffect> effects) => [.. effects.OrderBy(effect => effect.Name, CustomEffects.NameComparer)];
}
