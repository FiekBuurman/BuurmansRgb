using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Effects;

namespace BuurmansRgb.Engine.Lighting;

// The own effects by effect id, each with the effect that plays it: one for every device, as it
// keeps nothing between frames. Set fills the spare table from the current one and swaps them, so
// an effect with the same steps keeps its StepEffect. Until Set gave the own effects there are
// (effects.json out of reach, or no library at all) an own effect that is not here may still
// exist, so the lighting keeps its id and only shows the device color for the moment. Not
// thread-safe: LightingController uses it under its lock.
internal sealed class CustomEffectPlayers
{
    private Dictionary<string, Player> _players = new(StringComparer.Ordinal);
    private Dictionary<string, Player> _spare = new(StringComparer.Ordinal);

    // Whether Set gave the own effects there are.
    private bool _known;

    // The own effects there are now; before the first call no own effect counts as gone.
    public void Set(IReadOnlyList<CustomEffect> effects)
    {
        _known = true;
        (_players, _spare) = (_spare, _players);
        _players.Clear();
        foreach (var effect in effects)
        {
            _players[effect.Id] = _spare.TryGetValue(effect.Id, out var player) && player.Plays(effect)
                ? player with { Effect = effect }
                : new Player(effect, EffectCatalog.CreateCustom(effect));
        }

        _spare.Clear();
    }

    // What lighting with an own effect shows: its steps, and for one that is gone the device color:
    // only for a moment once the effects are known (see WithoutMissing), and until they are.
    public IEffect EffectOf(DeviceLighting lighting) =>
        _players.TryGetValue(lighting.EffectId, out var player) ? player.Steps : new StaticEffect(lighting.Color);

    // The state with the static effect in place of an own effect that does not exist (any more),
    // such as from a scene saved before it was deleted; the same state when there is none.
    public LightingState WithoutMissing(LightingState state)
    {
        if (!IsMissing(state.Global) && !state.Devices.Values.Any(IsMissing))
        {
            return state;
        }

        return state with
        {
            Global = WithoutMissing(state.Global),
            Devices = state.Devices.ToDictionary(pair => pair.Key, pair => WithoutMissing(pair.Value)),
        };
    }

    public DeviceLighting WithoutMissing(DeviceLighting lighting) =>
        IsMissing(lighting) ? lighting with { EffectId = EffectIds.Static } : lighting;

    public bool IsMissing(DeviceLighting lighting) =>
        _known && CustomEffects.IsCustom(lighting.EffectId) && !_players.ContainsKey(lighting.EffectId);

    public DeviceLighting ThrowIfMissing(DeviceLighting lighting) =>
        IsMissing(lighting) ? throw EffectCatalog.UnknownEffect(lighting.EffectId) : lighting;

    // An own effect and the StepEffect that plays its steps. Equal steps are compared one color at a
    // time, as an effect from the pipe is a new copy even when only its name changed.
    private readonly record struct Player(CustomEffect Effect, StepEffect Steps)
    {
        public bool Plays(CustomEffect effect) =>
            ReferenceEquals(Effect.Steps, effect.Steps) || Effect.Steps.SequenceEqual(effect.Steps);
    }
}
