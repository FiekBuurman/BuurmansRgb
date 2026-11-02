using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Lighting;

// The lines in engine.log at the start and the end of each reason to pause, so the log shows
// afterwards why the effects stood still: "Effects paused (screen off)." and "Pause ended (screen
// off); effects run again.", with the reasons that hold besides. Only a reason that starts or
// ends gets a line, so a state Windows tells again costs nothing.
internal static class PauseLog
{
    private static readonly (PauseReasons Reason, string Name)[] Names =
    [
        (PauseReasons.Manual, "tray menu"),
        (PauseReasons.Locked, "locked"),
        (PauseReasons.Asleep, "asleep"),
        (PauseReasons.DisplayOff, "screen off"),
        (PauseReasons.ScreenSaver, "screen saver"),
    ];

    // The line for reason that started or ended, with pause the reasons that hold afterwards.
    public static string Line(PauseReasons reason, bool started, PauseReasons pause)
    {
        if (started)
        {
            var others = pause & ~reason;
            return others == PauseReasons.None
                ? $"Effects paused ({Describe(reason)})."
                : $"Effects paused ({Describe(reason)}); also paused: {Describe(others)}.";
        }

        return pause == PauseReasons.None
            ? $"Pause ended ({Describe(reason)}); effects run again."
            : $"Pause ended ({Describe(reason)}); still paused: {Describe(pause)}.";
    }

    // The reasons in words, in the order of PauseReasons, comma-separated; "none" without any.
    public static string Describe(PauseReasons reasons)
    {
        var words = new List<string>(Names.Length);
        foreach (var (reason, name) in Names)
        {
            if (reasons.HasFlag(reason))
            {
                words.Add(name);
            }
        }

        return words.Count == 0 ? "none" : string.Join(", ", words);
    }
}
