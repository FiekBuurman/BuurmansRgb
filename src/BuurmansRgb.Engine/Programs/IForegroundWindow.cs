namespace BuurmansRgb.Engine.Programs;

// The program whose window is in the foreground. Windows tells it as it happens, without polling,
// but only while it is followed, so without a scene that asks for it this costs nothing.
internal interface IForegroundWindow
{
    // The exe name (any case) of the program whose window came to the foreground last while
    // followed; null before that and after Follow(false). Windows that only pass by or do not take
    // the user away from a program (the taskbar, Alt+Tab, the Start menu, the lock screen, the
    // engine's own) leave it as it was, and so does a window whose program cannot be read.
    string? Program { get; }

    // Raised when Program changes while followed, with the new value; also soon after Follow(true),
    // with the program in front then. On any thread.
    event EventHandler<string?>? Changed;

    // Starts or stops following. From any thread; it takes effect soon after, on the thread that
    // listens to Windows.
    void Follow(bool on);
}
