namespace BuurmansRgb.Engine.Profiles;

internal interface IProfileStore
{
    // The saved profiles, or none when there is no file yet.
    IReadOnlyList<Profile> Load();

    void Save(IReadOnlyList<Profile> profiles);
}
