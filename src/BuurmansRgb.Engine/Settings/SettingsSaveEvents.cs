namespace BuurmansRgb.Engine.Settings;

// Tells the connected settings apps when the lighting was written to the settings file, so they
// can show that a change is saved. The saver raises it on its timer thread.
internal sealed class SettingsSaveEvents
{
    public event EventHandler<bool>? Saved;

    public void Raise(bool succeeded) => Saved?.Invoke(this, succeeded);
}
