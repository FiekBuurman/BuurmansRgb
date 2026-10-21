using BuurmansRgb.Engine.Profiles;

namespace BuurmansRgb.Engine.Tests;

internal sealed class InMemoryProfileStore : IProfileStore
{
    public IReadOnlyList<Profile> Profiles { get; set; } = [];

    public Exception? LoadException { get; set; }

    public Exception? SaveException { get; set; }

    public int LoadCount { get; private set; }

    public IReadOnlyList<Profile> Load()
    {
        LoadCount++;
        if (LoadException is not null)
        {
            throw LoadException;
        }

        return Profiles;
    }

    public void Save(IReadOnlyList<Profile> profiles)
    {
        if (SaveException is not null)
        {
            throw SaveException;
        }

        Profiles = [.. profiles];
    }
}
