namespace BuurmansRgb.Engine.Profiles;

// What ProfileLibrary.AddStartScenes did, or for EngineHost why it did not ask.
internal enum StartScenesOutcome
{
    // There was no profiles.json: the start scenes are in it now.
    Written,

    // There was one already, also one without scenes or one that went aside as it did not read:
    // a pc that had scenes, so it gets none.
    FileThere,

    // profiles.json could not be written now, or EngineHost did not try as settings.json did not
    // read; a next start tries again.
    NotWritten,

    // Not asked (EngineHost only): startScenesCreated was settled before, or the engine runs without
    // scenes (no ProfileLibrary, in a test say). Nothing to record.
    AlreadySettled,
}
