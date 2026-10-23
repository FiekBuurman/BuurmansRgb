namespace BuurmansRgb.Contracts;

// Why the effects are paused; None while they run. Each reason ends on its own, so unlocking
// Windows does not end a pause from the tray menu, and resuming from the menu does not start the
// animation while Windows is locked.
[Flags]
public enum PauseReasons
{
    None = 0,

    // "Effecten pauzeren" in the tray menu. The engine leaves the devices as they are until it ends.
    Manual = 1,

    // Windows is locked, or the session is not on any screen (another user, or a remote desktop
    // that disconnected). Only the animation stops; the devices keep the last frame, or show it
    // dimmed or black (PausedLightsMode).
    Locked = 2,

    // The pc goes to sleep or hibernates; this ends when it wakes up.
    Asleep = 4,

    // The screen is off, also while Windows stays unlocked; this ends when it goes on again. A
    // dimmed screen does not count. Like Locked, only the animation stops. Asleep, DisplayOff and
    // ScreenSaver follow PausedLightsMode too.
    DisplayOff = 8,

    // The screen saver of Windows runs, also while Windows stays unlocked; this ends when it goes
    // away. Its preview in the screen saver settings does not count. Like DisplayOff.
    ScreenSaver = 16,
}
