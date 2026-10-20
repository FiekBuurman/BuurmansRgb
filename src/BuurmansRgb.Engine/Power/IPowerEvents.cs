namespace BuurmansRgb.Engine.Power;

internal interface IPowerEvents
{
    // Raised when Windows is about to sleep or hibernate, on any thread.
    event EventHandler? Suspending;

    // Raised when Windows wakes up from sleep or hibernation, on any thread.
    event EventHandler? Resumed;
}
