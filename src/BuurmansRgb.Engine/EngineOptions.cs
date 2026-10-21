namespace BuurmansRgb.Engine;

// The command line of the engine. The installer starts it once with --first-run, as the user
// who installed it, so that user gets "Starten met Windows" without opening the tray menu.
// --exit asks the engine that runs to exit as "Afsluiten" in the tray menu does, so a script
// need not kill it (which leaves its icon in the tray until the mouse passes over it); without
// a running engine it starts none.
internal sealed record EngineOptions(bool FirstRun, bool Exit = false)
{
    public const string FirstRunArgument = "--first-run";

    public const string ExitArgument = "--exit";

    public static EngineOptions Parse(IEnumerable<string> args)
    {
        var list = args.ToList();
        return new(
            list.Contains(FirstRunArgument, StringComparer.OrdinalIgnoreCase),
            list.Contains(ExitArgument, StringComparer.OrdinalIgnoreCase));
    }
}
