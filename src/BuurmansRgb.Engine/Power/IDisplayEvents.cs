namespace BuurmansRgb.Engine.Power;

internal interface IDisplayEvents
{
    // Whether the screen is off now, as Windows told last; false before it told anything. A dimmed
    // screen counts as on.
    bool IsOff { get; }

    // Raised when the screen went off or on again, with whether it is off now. Only for another
    // value, on any thread.
    event EventHandler<bool>? Switched;
}
