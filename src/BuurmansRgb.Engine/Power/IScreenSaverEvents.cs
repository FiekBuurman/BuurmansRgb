namespace BuurmansRgb.Engine.Power;

internal interface IScreenSaverEvents
{
    // Whether the screen saver of Windows runs now, as seen last; false before anything was seen.
    // Its preview in the settings of Windows does not count.
    bool IsRunning { get; }

    // Raised with true each time a window of a running screen saver comes to the front, also when
    // one ran already (AutoPause may have ended the pause on its own in between), and with false
    // once when another window comes to the front after it. On any thread.
    event EventHandler<bool>? Switched;
}
