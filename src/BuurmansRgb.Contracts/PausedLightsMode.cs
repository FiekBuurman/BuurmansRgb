namespace BuurmansRgb.Contracts;

// What the devices show while the effects pause because Windows is locked or asleep, or the screen
// is off. Keep leaves the last frame, Dim shows it at a low brightness and Off makes every device
// black. A manual pause from the tray menu leaves the devices alone whatever this is.
public enum PausedLightsMode
{
    Keep,
    Dim,
    Off,
}
