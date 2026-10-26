using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

internal sealed class InMemoryCustomEffectStore : ICustomEffectStore
{
    public IReadOnlyList<CustomEffect> Effects { get; set; } = [];

    public Exception? LoadException { get; set; }

    public Exception? SaveException { get; set; }

    public int LoadCount { get; private set; }

    public IReadOnlyList<CustomEffect> Load()
    {
        LoadCount++;
        if (LoadException is not null)
        {
            throw LoadException;
        }

        return Effects;
    }

    public void Save(IReadOnlyList<CustomEffect> effects)
    {
        if (SaveException is not null)
        {
            throw SaveException;
        }

        Effects = [.. effects];
    }
}
