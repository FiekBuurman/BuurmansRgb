namespace BuurmansRgb.UI.Services;

// The questions the settings app asks before it does something that does not come back
// (ConfirmDialog): true when the user said yes. The window makes them (WindowDialogs); tests give a
// fake one that answers itself.
public interface IConfirmDialogs
{
    // Before a scene is deleted, with its name.
    bool ConfirmDeleteScene(string name);

    // Before "Bijwerken met wat nu brandt" replaces the lighting of a scene, with its name.
    bool ConfirmUpdateScene(string name);

    // Before an own effect is deleted, with its name.
    bool ConfirmDeleteCustomEffect(string name);

    // "Je huidige instellingen worden vervangen…", with the name of the backup file.
    bool ConfirmRestore(string fileName);
}
