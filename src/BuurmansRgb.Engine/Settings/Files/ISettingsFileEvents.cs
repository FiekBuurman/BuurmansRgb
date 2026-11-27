namespace BuurmansRgb.Engine.Settings;

// When settings.json may have changed, by anyone: the engine's own saves too, and maybe several
// times for one change, as an editor may write a file in steps or to another file it then renames.
internal interface ISettingsFileEvents
{
    // Raised on any thread.
    event EventHandler? Changed;
}
