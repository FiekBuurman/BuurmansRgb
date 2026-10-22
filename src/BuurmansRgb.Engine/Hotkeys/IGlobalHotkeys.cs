namespace BuurmansRgb.Engine.Hotkeys;

// Keys that work anywhere in Windows, also while another app has the focus (RegisterHotKey).
// Call it on the tray thread only.
internal interface IGlobalHotkeys
{
    // A key with its modifiers, such as Keys.Control | Keys.Alt | Keys.A. pressed runs on the tray
    // thread each time it is pressed. False when another app has the key already.
    bool Register(Keys key, Action pressed);

    // Gives the key back to Windows; nothing happens when it is not registered.
    void Unregister(Keys key);
}
