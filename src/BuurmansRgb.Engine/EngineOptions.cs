namespace BuurmansRgb.Engine;

// The command line of the engine. The installer starts it once with --first-run, as the user
// who installed it, so that user gets "Starten met Windows" without opening the tray menu.
internal sealed record EngineOptions(bool FirstRun)
{
    public const string FirstRunArgument = "--first-run";

    public static EngineOptions Parse(IEnumerable<string> args) =>
        new(args.Contains(FirstRunArgument, StringComparer.OrdinalIgnoreCase));
}
