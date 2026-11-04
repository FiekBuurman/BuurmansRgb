namespace BuurmansRgb.Contracts;

// What IEngineApi.RestoreSettingsAsync did. Only Restored replaced anything.
public enum SettingsRestoreOutcome
{
    Restored,

    // Not a zip, a zip without any of the three files of SettingsBackup, or with anything else in
    // it (another file, a folder, a path): nothing replaced.
    NotABackup,

    // The zip, or a file in it once unpacked, is bigger than SettingsBackup allows.
    TooLarge,

    // A file in the zip is not one the engine can read (SettingsRestoreResult.FileName says which):
    // nothing replaced.
    Unreadable,

    // The files could not be written (no access, disk full): the engine put back what it had, and
    // the copy it made first is in the backups folder.
    Failed,
}
