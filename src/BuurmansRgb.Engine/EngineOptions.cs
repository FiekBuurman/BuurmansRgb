using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine;

// The command line of the engine. The installer starts it once with --first-run, as the user
// who installed it, so that user gets "Starten met Windows" without opening the tray menu.
// --exit asks the engine that runs to exit as "Afsluiten" in the tray menu does, so a script
// need not kill it (which leaves its icon in the tray until the mouse passes over it); without
// a running engine it starts none.
// "Herstarten" in the tray uses the other three: --restart-services is the step with admin
// rights, which restarts OpenRGB and PawnIO and ends with what it did as its exit code;
// --restarted <code> starts the new engine, which waits for the old one to exit and then tells
// what restarted; --open-settings opens the settings app once it runs, as it was open before.
internal sealed record EngineOptions(bool FirstRun, bool Exit = false)
{
    public const string FirstRunArgument = "--first-run";

    public const string ExitArgument = "--exit";

    public const string RestartServicesArgument = "--restart-services";

    public const string RestartedArgument = "--restarted";

    public const string OpenSettingsArgument = "--open-settings";

    public bool RestartServices { get; init; }

    // Set when "Herstarten" started this engine, with what the step with admin rights did.
    public ServicesRestartReport? Restarted { get; init; }

    public bool OpenSettings { get; init; }

    public static EngineOptions Parse(IEnumerable<string> args)
    {
        var list = args.ToList();
        var restartedAt = list.FindIndex(arg => string.Equals(arg, RestartedArgument, StringComparison.OrdinalIgnoreCase));
        return new(
            list.Contains(FirstRunArgument, StringComparer.OrdinalIgnoreCase),
            list.Contains(ExitArgument, StringComparer.OrdinalIgnoreCase))
        {
            RestartServices = list.Contains(RestartServicesArgument, StringComparer.OrdinalIgnoreCase),

            // Without a code that reads, still a restart, of which nothing is known.
            Restarted = restartedAt < 0
                ? null
                : ServicesRestartReport.ParseArgument(restartedAt + 1 < list.Count ? list[restartedAt + 1] : null) ?? ServicesRestartReport.Failed,
            OpenSettings = list.Contains(OpenSettingsArgument, StringComparer.OrdinalIgnoreCase),
        };
    }
}
