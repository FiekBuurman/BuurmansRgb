namespace BuurmansRgb.Engine.Power;

internal interface IPowerEvents
{
    // Raised when Windows wakes up from sleep or hibernation, on any thread.
    event EventHandler? Resumed;
}
