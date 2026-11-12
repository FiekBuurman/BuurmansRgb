using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Profiles;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine;

// What --scene "<name>", --all-on and --all-off ask, for a Stream Deck, Home Assistant or a
// script. A second start hands it to the engine that runs (SingleInstance.SendCommand) and ends
// with the answer as its exit code; without an engine, this start becomes the engine and does it
// once the saved lighting is on (TrayPresenter.Run).
internal sealed record EngineCommand(EngineCommandKind Kind, string Scene = "")
{
    public const string SceneArgument = "--scene";

    public const string AllOnArgument = "--all-on";

    public const string AllOffArgument = "--all-off";

    // How long a second start waits for its turn and the answer of the engine. Putting on a scene
    // takes a few milliseconds; more means the engine hangs.
    public static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(10);

    private const string ScenePrefix = "scene:";
    private const string AllOnMessage = "all-on";
    private const string AllOffMessage = "all-off";

    public static EngineCommand AllOn { get; } = new(EngineCommandKind.AllOn);

    public static EngineCommand AllOff { get; } = new(EngineCommandKind.AllOff);

    // The first of the three on the command line counts. A --scene without a name asks for the
    // scene "", which does not exist.
    public static EngineCommand? Parse(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (IsArgument(args[i], SceneArgument))
            {
                return new(EngineCommandKind.Scene, i + 1 < args.Count ? args[i + 1] : "");
            }

            if (IsArgument(args[i], AllOnArgument))
            {
                return AllOn;
            }

            if (IsArgument(args[i], AllOffArgument))
            {
                return AllOff;
            }
        }

        return null;
    }

    // As it goes to the engine that runs; a scene name can be at most 40 characters, so it always fits.
    public string ToMessage() => Kind switch
    {
        EngineCommandKind.Scene => ScenePrefix + Scene,
        EngineCommandKind.AllOn => AllOnMessage,
        _ => AllOffMessage,
    };

    // Null for a message this engine does not know, such as one of a newer version.
    public static EngineCommand? FromMessage(string message) =>
        message switch
        {
            AllOnMessage => AllOn,
            AllOffMessage => AllOff,
            _ when message.StartsWith(ScenePrefix, StringComparison.Ordinal) => new(EngineCommandKind.Scene, message[ScenePrefix.Length..]),
            _ => null,
        };

    // Hands the command to the engine that runs; its answer, or NoAnswer.
    public EngineCommandResult Send(string engineName) =>
        SingleInstance.SendCommand(engineName, ToMessage(), AnswerTimeout) is { } answer
            ? (EngineCommandResult)answer
            : EngineCommandResult.NoAnswer;

    // Before this start becomes the engine: a scene that is not there ends it at once, with a line
    // in the log, instead of an engine that keeps running and an exit code nobody gets. A file that
    // cannot be read now leaves it to the engine, which reports it once it runs.
    public EngineCommandResult CheckBeforeStart(IProfileStore profiles)
    {
        if (Kind != EngineCommandKind.Scene)
        {
            return EngineCommandResult.Done;
        }

        try
        {
            if (profiles.Load().Any(profile => Matches(profile.Name)))
            {
                return EngineCommandResult.Done;
            }
        }
        catch (Exception exception) when (JsonFile.IsAccessError(exception))
        {
            Trace.TraceError(exception.ToString());
            return EngineCommandResult.Done;
        }

        Trace.TraceWarning($"Command line: there is no scene '{Scene}'; the engine does not start.");
        return EngineCommandResult.UnknownScene;
    }

    // As the tray menu and the settings app: without regard to case or surrounding spaces.
    public bool Matches(string sceneName) => ProfileNames.Comparer.Equals(sceneName, Scene.Trim());

    public override string ToString() => Kind switch
    {
        EngineCommandKind.Scene => $"{SceneArgument} '{Scene}'",
        EngineCommandKind.AllOn => AllOnArgument,
        _ => AllOffArgument,
    };

    private static bool IsArgument(string arg, string name) => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase);
}

internal enum EngineCommandKind
{
    Scene,
    AllOn,
    AllOff,
}

// The exit code of a start with a command; docs/handleiding.md lists them.
internal enum EngineCommandResult
{
    Done = 0,

    // A device or profiles.json gave an error; the log of the engine tells which.
    Failed = 1,

    UnknownScene = 2,

    // The engine that runs has not put the saved lighting on yet, the first seconds after it started.
    NotStarted = 3,

    // No engine answered: an older one that does not know commands, or one that hangs.
    NoAnswer = 4,
}
