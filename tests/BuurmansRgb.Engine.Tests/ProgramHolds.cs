using BuurmansRgb.Engine.Profiles;

namespace BuurmansRgb.Engine.Tests;

internal static class ProgramHolds
{
    // A scene whose time came while the watcher holds it, as ProfileLibrary.LoadTimed does.
    public static bool HoldScene(this ProgramWatcher watcher, ProfileLibrary profiles, string scene) =>
        watcher.HoldUntilProgramsStop(_ => new HeldLighting(profiles.Get(scene).Lighting, scene));
}
