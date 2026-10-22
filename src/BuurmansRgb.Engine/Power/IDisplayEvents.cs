namespace BuurmansRgb.Engine.Power;

internal interface IDisplayEvents
{
    // Whether the screen is off now, as Windows told last; false before it told anything. A dimmed
    // screen counts as on.
    bool IsOff { get; }

    // Raised each time Windows tells the state, with whether the screen is off now; also when it
    // stays the same (on to dimmed, or off again after AutoPause ended the pause), on any thread.
    event EventHandler<bool>? Switched;
}
