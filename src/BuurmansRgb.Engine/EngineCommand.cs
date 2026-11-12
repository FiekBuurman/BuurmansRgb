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

    private const int MaxLogLength = 80;

    // Whether the command of Answer still waits for the tray thread, runs there, or came too late.
    private const int AnswerWaiting = 0;
    private const int AnswerRunning = 1;
    private const int AnswerGivenUp = 2;

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

    // In the engine that runs, on a thread of the pool: the answer to a message of a second start.
    // post puts an action on the tray thread, where run does the command as the menu does. Gives up
    // on the way out (shutdown, or post throws because the thread is gone) and when the tray thread
    // is stuck longer than timeout; the command does not run anymore then, unless it already began.
    public static EngineCommandResult Answer(
        string message,
        Action<Action> post,
        Func<EngineCommand, EngineCommandResult> run,
        TimeSpan timeout,
        CancellationToken shutdown)
    {
        if (FromMessage(message) is not { } command)
        {
            Trace.TraceWarning($"Command line: unknown command '{ForLog(message)}'.");
            return EngineCommandResult.Failed;
        }

        var done = new TaskCompletionSource<EngineCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = AnswerWaiting;
        try
        {
            shutdown.ThrowIfCancellationRequested();
            post(() =>
            {
                if (Interlocked.CompareExchange(ref state, AnswerRunning, AnswerWaiting) != AnswerWaiting)
                {
                    Trace.TraceWarning($"Command line: {command} came too late; nothing changed.");
                    return;
                }

                done.TrySetResult(run(command));
            });
            if (done.Task.Wait(timeout, shutdown))
            {
                return done.Task.Result;
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or InvalidOperationException)
        {
            // On the way out; the message loop of the tray thread may be gone already.
        }

        Interlocked.CompareExchange(ref state, AnswerGivenUp, AnswerWaiting);
        return EngineCommandResult.NoAnswer;
    }

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

        Trace.TraceWarning($"Command line: there is no scene '{ForLog(Scene)}'; the engine does not start.");
        return EngineCommandResult.UnknownScene;
    }

    // As the tray menu and the settings app: without regard to case or surrounding spaces.
    public bool Matches(string sceneName) => ProfileNames.Comparer.Equals(sceneName, Scene.Trim());

    // For engine.log; the scene as ForLog shows it.
    public override string ToString() => Kind switch
    {
        EngineCommandKind.Scene => $"{SceneArgument} '{ForLog(Scene)}'",
        EngineCommandKind.AllOn => AllOnArgument,
        _ => AllOffArgument,
    };

    // A name or message from the command line or a second start, for a line in engine.log: at most
    // MaxLogLength characters, and a control character (such as a line break, which would fake a
    // line of its own) as '?'. A scene name is at most 40 characters, so a real one stays whole.
    public static string ForLog(string text)
    {
        var shown = text.Length > MaxLogLength ? text[..MaxLogLength] : text;
        var chars = shown.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsControl(chars[i]))
            {
                chars[i] = '?';
            }
        }

        return text.Length > MaxLogLength ? new string(chars) + "…" : new string(chars);
    }

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
