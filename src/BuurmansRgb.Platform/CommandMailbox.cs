using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace BuurmansRgb.Platform;

// A command of a second start for the instance that runs, and its answer (the engine's --scene,
// --all-on and --all-off): Send hands it over, the first instance answers it through
// SingleInstance.ListenForCommand. The first instance holds the signal for commands from the
// moment it is acquired with acceptsCommands, so a sender finds it only while an instance runs
// that knows commands.
public sealed class CommandMailbox : IDisposable
{
    public const int MaxCommandBytes = 1024;

    // The command and its answer go through a small block of shared memory of the session, which
    // only exists while a command is on its way: the id of the command, the length of the command
    // in UTF-8 bytes, the answer with the id it is for, and the command. The answer and its id are
    // one aligned long, written and read in one go, so a sender never sees the answer to one
    // command with the id of another.
    private const int IdOffset = 0;
    private const int LengthOffset = 4;
    private const int AnswerOffset = 8;
    private const int CommandOffset = 16;
    private const int MailboxSize = CommandOffset + MaxCommandBytes;

    private readonly string _name;
    private readonly EventWaitHandle _command;
    private SignalListener? _listener;

    // Made at once by the first instance, not when it starts listening, so a command that comes
    // while it starts (a Stream Deck right after signing in) gets an answer (NotStarted until the
    // saved lighting is on) instead of none: the signal stays set until the listener comes.
    internal CommandMailbox(string name)
    {
        _name = name;
        _command = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, CommandName(name));
    }

    // Hands the running instance a command and waits at most timeout for its answer, which the
    // callback of ListenForCommand gives. Null at once when no instance runs that knows commands
    // (none runs, or an older one), and null after the timeout when none answered in time (one
    // that does not listen yet, or one that hangs). One command at a time
    // per session; the next waits its turn within its own timeout. Like
    // SingleInstance.SignalExitIfRunning it never creates the mutex, so an instance that starts
    // meanwhile is still the first.
    public static int? Send(string name, string command, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(command);
        var bytes = Encoding.UTF8.GetBytes(command);
        if (bytes.Length > MaxCommandBytes)
        {
            throw new ArgumentException($"A command is at most {MaxCommandBytes} bytes.", nameof(command));
        }

        // Named events and shared memory are Windows only, as the apps are.
        if (!OperatingSystem.IsWindows() || !EventWaitHandle.TryOpenExisting(CommandName(name), out var signal))
        {
            return null;
        }

        var waited = Stopwatch.StartNew();
        using (signal)
        using (var turn = new Mutex(initiallyOwned: false, TurnName(name)))
        {
            if (!WaitForTurn(turn, timeout))
            {
                return null;
            }

            try
            {
                using var answered = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, AnsweredName(name));
                answered.Reset();
                using var mailbox = MemoryMappedFile.CreateOrOpen(MailboxName(name), MailboxSize);
                using var view = mailbox.CreateViewAccessor();

                // A random id, never 0 (a new block) and another than the one before, so a late
                // answer to a command that gave up is not taken for this one, and a signal that
                // is left over does not run a command twice: the listener skips an id it answered.
                // A new block starts at 0 each time, so counting on from there would not do. The id
                // goes last, so a listener that sees it also sees the command.
                var id = NewCommandId(view.ReadInt32(IdOffset));
                view.Write(LengthOffset, bytes.Length);
                view.WriteArray(CommandOffset, bytes, 0, bytes.Length);
                Interlocked.MemoryBarrier();
                view.Write(IdOffset, id);
                signal.Set();

                while (true)
                {
                    var left = timeout - waited.Elapsed;
                    if (left <= TimeSpan.Zero || !answered.WaitOne(left))
                    {
                        return null;
                    }

                    var (answeredId, answer) = SplitAnswer(view.ReadInt64(AnswerOffset));
                    if (answeredId == id)
                    {
                        return answer;
                    }
                }
            }
            finally
            {
                turn.ReleaseMutex();
            }
        }
    }

    public void Dispose()
    {
        _listener?.Dispose();
        _command.Dispose();
    }

    // onCommand gets each command, on a thread of the pool, and gives the answer. A command that
    // came before this gets its answer as soon as it listens.
    internal SignalListener Listen(Func<string, int> onCommand) =>
        SignalListener.Start(_command, ref _listener, () => Answer(onCommand));

    private static string CommandName(string name) => $@"Local\{name}.Command";

    private static string TurnName(string name) => $@"Local\{name}.CommandTurn";

    private static string AnsweredName(string name) => $@"Local\{name}.CommandAnswered";

    private static string MailboxName(string name) => $@"Local\{name}.CommandMailbox";

    private static int NewCommandId(int previous)
    {
        while (true)
        {
            var id = Random.Shared.Next(1, int.MaxValue);
            if (id != previous)
            {
                return id;
            }
        }
    }

    private static long JoinAnswer(int id, int answer) => ((long)answer << 32) | (uint)id;

    private static (int Id, int Answer) SplitAnswer(long joined) => ((int)joined, (int)(joined >> 32));

    private static bool WaitForTurn(Mutex turn, TimeSpan timeout)
    {
        try
        {
            return turn.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            // A sender that stopped halfway left it; the turn is ours now.
            return true;
        }
    }

    // Reads the command, lets onCommand answer it and tells the sender. The mailbox is gone when
    // the sender gave up already: nobody waits for an answer then. Runs on a thread of the pool,
    // where an exception would end the process: any failure is only logged, and the sender gets
    // no answer.
    private void Answer(Func<string, int> onCommand)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            AnswerFromMailbox(onCommand);
        }
        catch (Exception exception)
        {
            Trace.TraceError($"A command of another start failed: {exception}");
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void AnswerFromMailbox(Func<string, int> onCommand)
    {
        MemoryMappedFile mailbox;
        try
        {
            mailbox = MemoryMappedFile.OpenExisting(MailboxName(_name));
        }
        catch (FileNotFoundException)
        {
            return;
        }

        using (mailbox)
        using (var view = mailbox.CreateViewAccessor())
        {
            // No id yet (the sender is still writing) or one that got its answer already (a signal
            // that was left over): nothing to do.
            var id = view.ReadInt32(IdOffset);
            if (id == 0 || id == SplitAnswer(view.ReadInt64(AnswerOffset)).Id)
            {
                return;
            }

            Interlocked.MemoryBarrier();
            var length = view.ReadInt32(LengthOffset);
            if (length is < 0 or > MaxCommandBytes)
            {
                return;
            }

            var bytes = new byte[length];
            view.ReadArray(CommandOffset, bytes, 0, length);
            var answer = onCommand(Encoding.UTF8.GetString(bytes));

            // Callbacks run side by side: one that took longer than its sender waited does not
            // overwrite the answer to the command after it.
            Interlocked.MemoryBarrier();
            if (view.ReadInt32(IdOffset) != id)
            {
                return;
            }

            view.Write(AnswerOffset, JoinAnswer(id, answer));
        }

        if (EventWaitHandle.TryOpenExisting(AnsweredName(_name), out var answered))
        {
            using (answered)
            {
                answered.Set();
            }
        }
    }
}
