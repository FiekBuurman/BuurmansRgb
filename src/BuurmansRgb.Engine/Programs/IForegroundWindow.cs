namespace BuurmansRgb.Engine.Programs;

// The program whose window is in the foreground. Windows tells it as it happens, without polling,
// but only while it is followed, so without a scene that asks for it this costs nothing.
internal interface IForegroundWindow
{
    // Raised while followed for each window that comes to the foreground and counts, with the exe
    // name (any case) of its program: also for another window of the same program, or a new start
    // of it, so the listener decides what is new. Windows that only pass by or do not take the user
    // away from a program (the taskbar, Alt+Tab, the Start menu, the lock screen, the engine's own)
    // raise nothing, and neither does a window whose program cannot be read. Also soon after
    // Follow(true), with the window in front then. On any thread.
    event EventHandler<string?>? Changed;

    // Starts or stops following. From any thread; it takes effect soon after, on the thread that
    // listens to Windows.
    void Follow(bool on);
}
