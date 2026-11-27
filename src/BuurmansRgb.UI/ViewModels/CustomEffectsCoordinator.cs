using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

// The own effects of the user ("Jouw effect") and the effects of the engine, as the effect choice
// on Kleur shows them: the editor on card 4 shows the chosen own effect, each change it makes goes
// to the engine, and what the engine has shows when connecting and after CustomEffectsChanged. A
// coordinator: it calls the engine of the session itself, and tells MainViewModel when the list of
// effects changed (EffectsChanged), which effect went (Deleted) or came by "+ Eigen effect maken"
// (Created), and what is for the status line (Message). Used on the UI thread.
internal sealed class CustomEffectsCoordinator
{
    // The kind of call in the call queue that reads the own effects again; it changes nothing.
    internal const string CustomEffectsReloadCall = "custom-effects-reload";

    // The kind of call that saves or deletes an own effect; with the id of the effect a newer one
    // replaces one that still waits.
    private const string CustomEffectCall = "custom-effect";

    private readonly EngineSession _session;
    private readonly CustomEffectEditorViewModel _editor;
    private readonly Func<EffectOption?> _selectedEffect;

    // The effects of the engine itself, and the own effects of the user, in alphabetical order of
    // their names, as the engine gave them and with the changes sent since.
    private IReadOnlyList<string> _engineEffects = [];
    private IReadOnlyList<CustomEffect> _customEffects = [];

    // Counts the changes to own effects sent from here, so a list fetched while the user changed
    // one does not bring back an older version.
    private int _customEffectChanges;

    // selectedEffect is the effect chosen on Kleur. The editor checks a name against the effects
    // known here (IsNameTaken), so the coordinator makes it.
    public CustomEffectsCoordinator(EngineSession session, Func<EffectOption?> selectedEffect)
    {
        _session = session;
        _selectedEffect = selectedEffect;
        _editor = new CustomEffectEditorViewModel(IsNameTaken);
        _editor.Changed += (_, effect) => Send(effect);
        _editor.DeleteRequested += (_, id) => Delete(id);
    }

    // The editor of the chosen own effect on card 4.
    public CustomEffectEditorViewModel Editor => _editor;

    // The effects the effect choice shows changed: MainViewModel shows Options again.
    public event EventHandler? EffectsChanged;

    // The own effect with this id is deleted here, and so no longer in Options; the engine gives
    // every device that shows it the static effect.
    public event EventHandler<string>? Deleted;

    // The new own effect with this id is saved by the engine and in Options.
    public event EventHandler<string>? Created;

    // For the status line, such as "Effect … verwijderd".
    public event EventHandler<string>? Message;

    // The effects of the engine and then the own effects, under the names the effect choice shows
    // them with.
    public IReadOnlyList<EffectOption> Options =>
        [.. _engineEffects.Select(EffectOption.Create), .. _customEffects.Select(EffectOption.For)];

    // "+ Eigen effect maken" works while connected, up to CustomEffects.MaxEffects.
    public bool CanCreate => _session.HasConnection && _customEffects.Count < CustomEffects.MaxEffects;

    // When connecting, after the devices. An engine that cannot read effects.json yet gives no own
    // effects (GetCustomEffectsOrNoneAsync).
    public async Task LoadAsync(IEngineApi api)
    {
        _engineEffects = await api.GetEffectsAsync();
        _customEffects = await GetCustomEffectsOrNoneAsync(api);
        EffectsChanged?.Invoke(this, EventArgs.Empty);
    }

    // Another settings app saved or deleted an effect, or this one did. In the queue, behind the
    // changes of this app that wait, and only shown when no change was sent meanwhile.
    public void Reload()
    {
        var changes = _customEffectChanges;
        _session.Read(CustomEffectsReloadCall, api => api.GetCustomEffectsAsync(), effects =>
        {
            if (changes == _customEffectChanges)
            {
                _customEffects = effects;
                EffectsChanged?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    // The editor shows the chosen own effect as it is known here. True when it shows another effect
    // than before, so the picker follows its step.
    public bool ShowInEditor(EffectOption? option)
    {
        if (option is not { IsCustom: true } || _customEffects.FirstOrDefault(effect => effect.Id == option.Id) is not { } effect)
        {
            return false;
        }

        var other = effect.Id != _editor.EffectId;
        _editor.Show(effect);
        return other;
    }

    // "LED-kleuren van dit apparaat overnemen": the colors the per-LED effect gives the LEDs of the
    // device become the pattern of the selected step, unless the editor shows another effect by
    // the time they come.
    public async Task TakeLedPatternAsync(string deviceId)
    {
        if (_session.Current is not { } connection)
        {
            return;
        }

        var effectId = _editor.EffectId;
        var (ok, colors) = await _session.TryCallAsync(connection, api => api.GetLedColorsAsync(deviceId));
        if (ok && _session.IsCurrent(connection) && _editor.EffectId == effectId && _selectedEffect()?.IsCustom == true)
        {
            _editor.ShowPattern(colors);
        }
    }

    // "+ Eigen effect maken": a new effect of two steps, color and black, a second each with a second
    // to fade, under the first free name "Eigen effect 1", "Eigen effect 2", ... Once the engine has
    // saved it, it is in Options and Created says so.
    public void Create(RgbColor color)
    {
        if (!_session.HasConnection)
        {
            return;
        }

        var name = CustomEffects.FirstFreeName(
            number => UiText.Format(Strings.CustomEffectNewName, number),
            candidate => _customEffects.Any(effect => CustomEffects.NameComparer.Equals(effect.Name, candidate)));
        var effect = new CustomEffect(string.Empty, name, [new CustomEffectStep(color), new CustomEffectStep(RgbColor.Black)]);
        _customEffectChanges++;
        _session.PostOn(new object(), async connection =>
        {
            var saved = await CallOrShowCustomEffectsAsync(connection, () => connection.Api.SaveCustomEffectAsync(effect));
            _session.WhenCurrent(connection, () =>
            {
                Show(saved);
                Created?.Invoke(this, saved.Id);
            });
        });
    }

    // Whether another own effect, or an effect of the engine, has the name in the effect choice;
    // the engine refuses both (CustomEffectLibrary), so the editor says so before it asks.
    public bool IsNameTaken(string id, string name) =>
        _customEffects.Any(effect => effect.Id != id && CustomEffects.NameComparer.Equals(effect.Name, name))
        || _engineEffects.Any(engineEffect => CustomEffects.NameComparer.Equals(EffectOption.NameOf(engineEffect), name));

    // Each change of the editor goes out as the whole effect; a newer one replaces one that waits.
    // A new name shows in the effect choice at once.
    private void Send(CustomEffect effect)
    {
        if (!_session.HasConnection)
        {
            return;
        }

        _customEffectChanges++;
        Show(effect);
        _session.PostOn((CustomEffectCall, effect.Id), connection => CallOrShowCustomEffectsAsync(connection, () => connection.Api.SaveCustomEffectAsync(effect)));
    }

    // The engine gives every device that shows it, and so this one, the static effect; Deleted lets
    // the choice go there at once, which hides the editor. With the same key, it replaces a change
    // of the effect that still waits.
    private void Delete(string id)
    {
        if (!_session.HasConnection || _customEffects.FirstOrDefault(effect => effect.Id == id) is not { } deleted)
        {
            return;
        }

        _customEffectChanges++;
        _customEffects = [.. _customEffects.Where(effect => effect.Id != id)];
        Deleted?.Invoke(this, id);
        _session.PostOn((CustomEffectCall, id), async connection =>
        {
            await CallOrShowCustomEffectsAsync(connection, async () =>
            {
                await connection.Api.DeleteCustomEffectAsync(id);
                return true;
            });
            _session.WhenCurrent(connection, () => Message?.Invoke(this, UiText.Format(Strings.CustomEffectDeleted, deleted.Name)));
        });
    }

    // An effect as this app sent it or the engine saved it, in its place in the alphabet.
    private void Show(CustomEffect effect)
    {
        _customEffects = [.. _customEffects.Where(other => other.Id != effect.Id).Append(effect).OrderBy(other => other.Name, CustomEffects.NameComparer)];
        EffectsChanged?.Invoke(this, EventArgs.Empty);
    }

    // A change of an own effect the engine refused, such as a name another settings app took
    // meanwhile or an effect it deleted: the effects as the engine keeps them replace the ones shown
    // here, without the check of Reload, as the list it sent may have been dropped for this very
    // change. The error still goes on to OnCallFailed.
    private async Task<T> CallOrShowCustomEffectsAsync<T>(IEngineConnection connection, Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (Exception exception) when (EngineRpc.IsRejectedByEngine(exception))
        {
            // A reload that fails too only gets logged, so the refusal still reaches OnCallFailed.
            try
            {
                var effects = await connection.Api.GetCustomEffectsAsync();
                _session.WhenCurrent(connection, () =>
                {
                    _customEffects = effects;
                    EffectsChanged?.Invoke(this, EventArgs.Empty);
                });
            }
            catch (Exception reloadException)
            {
                Trace.TraceWarning(reloadException.ToString());
            }

            throw;
        }
    }

    // An engine that cannot read effects.json yet, as another program holds it, refuses the
    // question; the app connects without own effects then, and shows them at the
    // CustomEffectsChanged the engine sends once it has read the file.
    private static async Task<IReadOnlyList<CustomEffect>> GetCustomEffectsOrNoneAsync(IEngineApi api)
    {
        try
        {
            return await api.GetCustomEffectsAsync();
        }
        catch (Exception exception) when (EngineRpc.IsRejectedByEngine(exception))
        {
            Trace.TraceWarning(exception.ToString());
            return [];
        }
    }
}
