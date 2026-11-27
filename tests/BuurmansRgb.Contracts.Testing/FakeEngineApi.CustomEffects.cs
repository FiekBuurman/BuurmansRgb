using System.Globalization;

namespace BuurmansRgb.Contracts.Testing;

// The effects the user makes ("Jouw effect").
public sealed partial class FakeEngineApi
{
    public event EventHandler? CustomEffectsChanged;

    public void RaiseCustomEffectsChanged() => CustomEffectsChanged?.Invoke(this, EventArgs.Empty);

    public bool HasCustomEffectsSubscribers => CustomEffectsChanged is not null;

    // Like the engine: in alphabetical order of their names; a new one gets the next id.
    public List<CustomEffect> CustomEffects { get; set; } = [];

    private int _nextCustomEffect = 1;

    // When set, a new effect gets this id instead of the next number.
    public string? NewCustomEffectId { get; set; }

    // Such as the engine that cannot read effects.json yet.
    public Exception? GetCustomEffectsException { get; set; }

    public Task<IReadOnlyList<CustomEffect>> GetCustomEffectsAsync(CancellationToken ct = default) =>
        GetCustomEffectsException is { } exception
            ? Task.FromException<IReadOnlyList<CustomEffect>>(exception)
            : Task.FromResult<IReadOnlyList<CustomEffect>>([.. CustomEffects]);

    public async Task<CustomEffect> SaveCustomEffectAsync(CustomEffect effect, CancellationToken ct = default)
    {
        var saved = effect.Id.Length == 0 ? effect with { Id = NewCustomEffectId ?? $"custom:{_nextCustomEffect++}" } : effect;
        await Record($"custom-effect save {saved.Id} {saved.Name} {string.Join(" ", saved.Steps.Select(Describe))}");
        CustomEffects = [.. CustomEffects.Where(other => other.Id != saved.Id).Append(saved).OrderBy(other => other.Name, StringComparer.CurrentCultureIgnoreCase)];
        return saved;
    }

    public async Task DeleteCustomEffectAsync(string id, CancellationToken ct = default)
    {
        await Record($"custom-effect delete {id}");
        CustomEffects.RemoveAll(effect => effect.Id == id);
    }

    // "#FF0000/1/0" for a step of one color, "3leds/1/0" for a pattern.
    public static string Describe(CustomEffectStep step) => string.Create(
        CultureInfo.InvariantCulture,
        $"{(step.IsPattern ? $"{step.Colors.Count}leds" : step.Colors[0].ToHex())}/{step.HoldSeconds}/{step.FadeSeconds}");
}
