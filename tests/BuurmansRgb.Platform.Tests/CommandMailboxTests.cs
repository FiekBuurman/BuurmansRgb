namespace BuurmansRgb.Platform.Tests;

public sealed class CommandMailboxTests
{
    private readonly string _name = $"BuurmansRgb.Tests.{Guid.NewGuid():N}";

    // The engine's --scene, --all-on and --all-off: the answer becomes the exit code.
    [Fact]
    public async Task Send_PrimaryListens_GetsTheCommandAndGivesItsAnswer()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);
        var received = new List<string>();
        using var listening = primary.ListenForCommand(command =>
        {
            lock (received)
            {
                received.Add(command);
            }

            return command == "scène:Café" ? 7 : 0;
        });

        var answer = await Task.Run(() => CommandMailbox.Send(_name, "scène:Café", TestTimeouts.Short), Ct);

        Assert.Equal(7, answer);
        Assert.Equal(["scène:Café"], received);
    }

    [Fact]
    public async Task Send_OneAfterTheOther_EachGetsItsOwnAnswer()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);
        using var listening = primary.ListenForCommand(command => command.Length);

        var answers = await Task.Run(() => new[] { "a", "bb", "ccc" }.Select(command => CommandMailbox.Send(_name, command, TestTimeouts.Short)).ToList(), Ct);

        Assert.Equal([1, 2, 3], answers);
    }

    [Fact]
    public async Task Send_AtTheSameTime_EachGetsItsOwnAnswer()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);
        using var listening = primary.ListenForCommand(command => int.Parse(command, System.Globalization.CultureInfo.InvariantCulture));

        var answers = await Task.WhenAll(Enumerable.Range(1, 5).Select(number => Task.Run(() => CommandMailbox.Send(_name, number.ToString(System.Globalization.CultureInfo.InvariantCulture), TestTimeouts.Short), Ct)));

        Assert.Equal([1, 2, 3, 4, 5], answers);
    }

    // An engine that is still starting: the command waits for it, and is not lost.
    [Fact]
    public async Task Send_BeforePrimaryListens_GetsTheAnswerOnceItListens()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);

        var answer = Task.Run(() => CommandMailbox.Send(_name, "all-on", TestTimeouts.Short), Ct);

        // No signal exists for "still waiting": this pause can only let a broken wait stay green,
        // never turn a good one red.
        await Task.Delay(TimeSpan.FromMilliseconds(200), Ct);
        Assert.False(answer.IsCompleted);
        using var listening = primary.ListenForCommand(command => command == "all-on" ? 3 : 0);

        Assert.Equal(3, await answer);
    }

    [Fact]
    public async Task Send_PrimaryNeverListens_IsNullAfterTheTimeout()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);

        var answer = await Task.Run(() => CommandMailbox.Send(_name, "all-on", TimeSpan.FromMilliseconds(200)), Ct);

        Assert.Null(answer);
    }

    // An instance of before commands holds the name, but made no signal for them.
    [Fact]
    public void Send_OlderInstanceRuns_IsNullAtOnce()
    {
        using var older = new Mutex(initiallyOwned: false, $@"Local\{_name}");

        Assert.Null(CommandMailbox.Send(_name, "all-on", TestTimeouts.Short));
        Assert.Null(CommandMailbox.Send($"{_name}.Other", "all-on", TestTimeouts.Short));
    }

    // The settings app: the first one, but not one that takes commands.
    [Fact]
    public void Send_InstanceThatTakesNoCommands_IsNullAtOnce()
    {
        using var settingsApp = SingleInstance.Acquire(_name);
        var waited = System.Diagnostics.Stopwatch.StartNew();

        Assert.Null(CommandMailbox.Send(_name, "all-on", TimeSpan.FromSeconds(5)));
        Assert.True(waited.Elapsed < TimeSpan.FromSeconds(2), $"Waited {waited.Elapsed}.");
    }

    // It only looks, as SignalExitIfRunning: an instance that starts right after it is the first.
    [Fact]
    public void Send_NoneRunning_CreatesNothing()
    {
        Assert.Null(CommandMailbox.Send(_name, "all-on", TestTimeouts.Short));

        using var next = SingleInstance.Acquire(_name, acceptsCommands: true);
        Assert.True(next.IsPrimary);
    }

    [Fact]
    public async Task Send_NoAnswerInTime_IsNull_AndTheNextGetsItsOwnAnswer()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);
        using var release = new ManualResetEventSlim();
        using var listening = primary.ListenForCommand(command =>
        {
            if (command == "slow")
            {
                release.Wait(TestTimeouts.Short);
                return 1;
            }

            return 2;
        });

        var slow = await Task.Run(() => CommandMailbox.Send(_name, "slow", TimeSpan.FromMilliseconds(100)), Ct);
        var next = Task.Run(() => CommandMailbox.Send(_name, "fast", TestTimeouts.Short), Ct);
        await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        release.Set();

        Assert.Null(slow);
        Assert.Equal(2, await next);
    }

    // Callbacks run side by side: the slow one finishes while the next is being answered, and
    // leaves the answer alone (the block is new, so still empty), so it cannot mix with the next.
    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task Send_LateAnswer_DoesNotOverwriteTheNext()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);
        using var mailbox = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateOrOpen($@"Local\{_name}.CommandMailbox", 16 + CommandMailbox.MaxCommandBytes);
        using var view = mailbox.CreateViewAccessor();
        using var releaseSlow = new ManualResetEventSlim();
        long answerSlotAfterSlow = -1;
        using var listening = primary.ListenForCommand(command =>
        {
            if (command == "slow")
            {
                releaseSlow.Wait(TestTimeouts.Short);
                return 41;
            }

            releaseSlow.Set();
            Thread.Sleep(TimeSpan.FromMilliseconds(300)); // the slow one writes, or would, meanwhile
            Volatile.Write(ref answerSlotAfterSlow, view.ReadInt64(8));
            return 2;
        });

        var slow = await Task.Run(() => CommandMailbox.Send(_name, "slow", TimeSpan.FromMilliseconds(100)), Ct);
        var next = await Task.Run(() => CommandMailbox.Send(_name, "fast", TestTimeouts.Short), Ct);

        Assert.Null(slow);
        Assert.Equal(2, next);
        Assert.Equal(0, Volatile.Read(ref answerSlotAfterSlow)); // nothing answered yet in this block
    }

    // On a thread of the pool, where an exception would end the process (and this test run).
    [Fact]
    public async Task Send_CallbackThrows_IsNull_AndTheListenerKeepsAnswering()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);
        using var listening = primary.ListenForCommand(command => command == "bad" ? throw new IOException("Kapot") : 5);

        var bad = await Task.Run(() => CommandMailbox.Send(_name, "bad", TimeSpan.FromMilliseconds(300)), Ct);
        var good = await Task.Run(() => CommandMailbox.Send(_name, "good", TestTimeouts.Short), Ct);

        Assert.Null(bad);
        Assert.Equal(5, good);
    }

    // A signal that is left over, while the shared block stays (here held open by the test, as a
    // sender that is just starting would), does not run the last command again.
    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task Send_LeftOverSignal_DoesNotRunTheCommandTwice()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);
        var calls = 0;
        using var listening = primary.ListenForCommand(command =>
        {
            Interlocked.Increment(ref calls);
            return command.Length;
        });
        using var mailbox = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateOrOpen($@"Local\{_name}.CommandMailbox", 16 + CommandMailbox.MaxCommandBytes);

        Assert.Equal(1, await Task.Run(() => CommandMailbox.Send(_name, "a", TestTimeouts.Short), Ct));
        using (var signal = EventWaitHandle.OpenExisting($@"Local\{_name}.Command"))
        {
            signal.Set();
        }

        await Task.Delay(TimeSpan.FromMilliseconds(200), Ct);
        Assert.Equal(1, Volatile.Read(ref calls));
        Assert.Equal(2, await Task.Run(() => CommandMailbox.Send(_name, "bb", TestTimeouts.Short), Ct));
        Assert.Equal(2, Volatile.Read(ref calls));
    }

    // A --scene of another version: the turn, the answer signal and the mailbox exist while a
    // command is on its way, by these names.
    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task Send_KernelObjectNames_AreFixed()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);
        var found = new List<string>();
        using var listening = primary.ListenForCommand(_ =>
        {
            if (Mutex.TryOpenExisting($@"Local\{_name}.CommandTurn", out var turn))
            {
                turn.Dispose();
                found.Add("CommandTurn");
            }

            if (EventWaitHandle.TryOpenExisting($@"Local\{_name}.CommandAnswered", out var answered))
            {
                answered.Dispose();
                found.Add("CommandAnswered");
            }

            try
            {
                System.IO.MemoryMappedFiles.MemoryMappedFile.OpenExisting($@"Local\{_name}.CommandMailbox").Dispose();
                found.Add("CommandMailbox");
            }
            catch (FileNotFoundException)
            {
            }

            return 1;
        });

        Assert.Equal(1, await Task.Run(() => CommandMailbox.Send(_name, "all-on", TestTimeouts.Short), Ct));

        Assert.Equal(["CommandTurn", "CommandAnswered", "CommandMailbox"], found);
    }

    // The mailbox byte for byte, as a sender of another version writes it: the id at 0, the length
    // in UTF-8 bytes at 4, the answer at 8 (the id in the low half, the answer in the high half) and
    // the command at 16, at most 1024 bytes.
    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task Send_MailboxLayout_IsFixed()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);
        using var listening = primary.ListenForCommand(command => command == "scène:Café" ? 7 : 0);
        using var mailbox = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateOrOpen($@"Local\{_name}.CommandMailbox", 16 + 1024);
        using var view = mailbox.CreateViewAccessor();

        Assert.Equal(7, await Task.Run(() => CommandMailbox.Send(_name, "scène:Café", TestTimeouts.Short), Ct));

        var expected = System.Text.Encoding.UTF8.GetBytes("scène:Café");
        var id = view.ReadInt32(0);
        var command = new byte[expected.Length];
        view.ReadArray(16, command, 0, command.Length);
        Assert.NotEqual(0, id);
        Assert.Equal(expected.Length, view.ReadInt32(4));
        Assert.Equal(expected, command);
        Assert.Equal((7L << 32) | (uint)id, view.ReadInt64(8));
        Assert.Equal(1024, CommandMailbox.MaxCommandBytes);
    }

    // The other way round: the listener answers a command written by hand in that layout.
    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task ListenForCommand_CommandInTheFixedLayout_IsAnswered()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);
        using var listening = primary.ListenForCommand(command => command == "all-off" ? 4 : 0);
        using var answered = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, $@"Local\{_name}.CommandAnswered");
        using var mailbox = System.IO.MemoryMappedFiles.MemoryMappedFile.CreateOrOpen($@"Local\{_name}.CommandMailbox", 16 + 1024);
        using var view = mailbox.CreateViewAccessor();
        var bytes = System.Text.Encoding.UTF8.GetBytes("all-off");

        view.Write(4, bytes.Length);
        view.WriteArray(16, bytes, 0, bytes.Length);
        view.Write(0, 12345);
        using (var signal = EventWaitHandle.OpenExisting($@"Local\{_name}.Command"))
        {
            signal.Set();
        }

        Assert.True(await Task.Run(() => answered.WaitOne(TestTimeouts.Short), Ct));
        Assert.Equal((4L << 32) | 12345u, view.ReadInt64(8));
    }

    [Fact]
    public void Send_TooLong_Throws()
    {
        Assert.Throws<ArgumentException>(() => CommandMailbox.Send(_name, new string('x', CommandMailbox.MaxCommandBytes + 1), TestTimeouts.Short));
    }

    [Fact]
    public async Task ListenForCommand_AfterListenerIsDisposed_NoAnswer()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);
        primary.ListenForCommand(_ => 0).Dispose();

        var answer = await Task.Run(() => CommandMailbox.Send(_name, "all-on", TimeSpan.FromMilliseconds(200)), Ct);

        Assert.Null(answer);
    }
}
