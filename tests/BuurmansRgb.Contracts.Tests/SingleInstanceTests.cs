using BuurmansRgb.Contracts;

namespace BuurmansRgb.Contracts.Tests;

public sealed class SingleInstanceTests
{
    private readonly string _name = $"BuurmansRgb.Tests.{Guid.NewGuid():N}";

    [Fact]
    public void Acquire_FirstInstance_IsPrimary()
    {
        using var first = SingleInstance.Acquire(_name);

        Assert.True(first.IsPrimary);
    }

    [Fact]
    public void Acquire_WhileFirstIsAlive_IsNotPrimary()
    {
        using var first = SingleInstance.Acquire(_name);
        using var second = SingleInstance.Acquire(_name);

        Assert.False(second.IsPrimary);
    }

    [Fact]
    public void Acquire_AfterFirstIsDisposed_IsPrimaryAgain()
    {
        SingleInstance.Acquire(_name).Dispose();

        using var next = SingleInstance.Acquire(_name);

        Assert.True(next.IsPrimary);
    }

    // The engine that "Herstarten" starts waits for the old one to exit.
    [Fact]
    public async Task AcquireWhenFree_FirstGoesAway_BecomesPrimary()
    {
        var first = SingleInstance.Acquire(_name);
        var release = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), Ct);
            first.Dispose();
        }, Ct);

        using var next = SingleInstance.AcquireWhenFree(_name, TestTimeouts.Short, TimeSpan.FromMilliseconds(20));

        await release;
        Assert.True(next.IsPrimary);
    }

    [Fact]
    public void AcquireWhenFree_FirstStays_GivesUpAfterTheTimeout()
    {
        using var first = SingleInstance.Acquire(_name);

        using var next = SingleInstance.AcquireWhenFree(_name, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(20));

        Assert.False(next.IsPrimary);
        Assert.True(first.IsPrimary);
    }

    [Fact]
    public async Task SignalExitIfRunning_Running_AsksItToExit()
    {
        using var primary = SingleInstance.Acquire(_name);
        var exited = new TaskCompletionSource();
        using var listening = primary.ListenForExit(() => exited.TrySetResult());

        Assert.True(SingleInstance.SignalExitIfRunning(_name));

        await exited.Task.WaitAsync(TestTimeouts.Short, Ct);
    }

    // It only looks: an instance that starts right after it is still the first.
    [Fact]
    public void SignalExitIfRunning_NoneRunning_CreatesNothing()
    {
        Assert.False(SingleInstance.SignalExitIfRunning(_name));

        Assert.False(Mutex.TryOpenExisting($@"Local\{_name}", out _));
        using var next = SingleInstance.Acquire(_name);
        Assert.True(next.IsPrimary);
    }

    [Fact]
    public async Task SignalPrimary_InvokesActivationCallbackOfPrimary()
    {
        using var primary = SingleInstance.Acquire(_name);
        var activated = new TaskCompletionSource();
        primary.ListenForActivation(() => activated.TrySetResult());

        using (var second = SingleInstance.Acquire(_name))
        {
            second.SignalPrimary();
        }

        await activated.Task.WaitAsync(TestTimeouts.Short, Ct);
    }

    [Fact]
    public async Task SignalPrimary_BeforePrimaryListens_IsNotLost()
    {
        using var primary = SingleInstance.Acquire(_name);
        using (var second = SingleInstance.Acquire(_name))
        {
            second.SignalPrimary();
        }

        var activated = new TaskCompletionSource();
        primary.ListenForActivation(() => activated.TrySetResult());

        await activated.Task.WaitAsync(TestTimeouts.Short, Ct);
    }

    [Fact]
    public async Task ListenForActivation_AfterListenerIsDisposed_OldCallbackIsNotInvoked()
    {
        using var primary = SingleInstance.Acquire(_name);
        var oldCalls = 0;
        primary.ListenForActivation(() => Interlocked.Increment(ref oldCalls)).Dispose();

        var activated = new TaskCompletionSource();
        using var listener = primary.ListenForActivation(() => activated.TrySetResult());
        using (var second = SingleInstance.Acquire(_name))
        {
            second.SignalPrimary();
        }

        await activated.Task.WaitAsync(TestTimeouts.Short, Ct);
        Assert.Equal(0, Volatile.Read(ref oldCalls));
    }

    [Fact]
    public async Task ListenerDispose_WaitsForRunningCallback()
    {
        using var primary = SingleInstance.Acquire(_name);
        using var callbackStarted = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        var callbackFinished = false;
        var listener = primary.ListenForActivation(() =>
        {
            callbackStarted.Set();
            releaseCallback.Wait(TestTimeouts.Short);
            Volatile.Write(ref callbackFinished, true);
        });
        primary.SignalPrimary();
        Assert.True(callbackStarted.Wait(TestTimeouts.Short, Ct));

        var dispose = Task.Run(listener.Dispose, Ct);
        await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        Assert.False(dispose.IsCompleted);
        releaseCallback.Set();
        await dispose.WaitAsync(TestTimeouts.Short, Ct);

        Assert.True(Volatile.Read(ref callbackFinished));
    }

    [Fact]
    public async Task ListenerDispose_FromInsideCallback_DoesNotDeadlock()
    {
        using var primary = SingleInstance.Acquire(_name);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable? listener = null;
        listener = primary.ListenForActivation(() =>
        {
            Volatile.Read(ref listener)!.Dispose();
            disposed.TrySetResult();
        });

        primary.SignalPrimary();

        await disposed.Task.WaitAsync(TestTimeouts.Short, Ct);
    }

    [Fact]
    public async Task SignalPrimaryToExit_InvokesOnlyTheExitCallbackOfPrimary()
    {
        using var primary = SingleInstance.Acquire(_name);
        var activated = false;
        var exited = new TaskCompletionSource();
        using var activation = primary.ListenForActivation(() => activated = true);
        using var exit = primary.ListenForExit(() => exited.TrySetResult());

        using (var second = SingleInstance.Acquire(_name))
        {
            second.SignalPrimaryToExit();
        }

        await exited.Task.WaitAsync(TestTimeouts.Short, Ct);
        Assert.False(activated);
    }

    // The engine's --scene, --all-on and --all-off: the answer becomes the exit code.
    [Fact]
    public async Task SendCommand_PrimaryListens_GetsTheCommandAndGivesItsAnswer()
    {
        using var primary = SingleInstance.Acquire(_name);
        var received = new List<string>();
        using var listening = primary.ListenForCommand(command =>
        {
            lock (received)
            {
                received.Add(command);
            }

            return command == "scène:Café" ? 7 : 0;
        });

        var answer = await Task.Run(() => SingleInstance.SendCommand(_name, "scène:Café", TestTimeouts.Short), Ct);

        Assert.Equal(7, answer);
        Assert.Equal(["scène:Café"], received);
    }

    [Fact]
    public async Task SendCommand_OneAfterTheOther_EachGetsItsOwnAnswer()
    {
        using var primary = SingleInstance.Acquire(_name);
        using var listening = primary.ListenForCommand(command => command.Length);

        var answers = await Task.Run(() => new[] { "a", "bb", "ccc" }.Select(command => SingleInstance.SendCommand(_name, command, TestTimeouts.Short)).ToList(), Ct);

        Assert.Equal([1, 2, 3], answers);
    }

    [Fact]
    public async Task SendCommand_AtTheSameTime_EachGetsItsOwnAnswer()
    {
        using var primary = SingleInstance.Acquire(_name);
        using var listening = primary.ListenForCommand(command => int.Parse(command, System.Globalization.CultureInfo.InvariantCulture));

        var answers = await Task.WhenAll(Enumerable.Range(1, 5).Select(number => Task.Run(() => SingleInstance.SendCommand(_name, number.ToString(System.Globalization.CultureInfo.InvariantCulture), TestTimeouts.Short), Ct)));

        Assert.Equal([1, 2, 3, 4, 5], answers);
    }

    // No engine, or one of before this command that only listens for activation and exit.
    [Fact]
    public void SendCommand_NoneListens_IsNullAtOnce()
    {
        using var primary = SingleInstance.Acquire(_name);
        using var activation = primary.ListenForActivation(() => { });

        Assert.Null(SingleInstance.SendCommand(_name, "all-on", TestTimeouts.Short));
        Assert.Null(SingleInstance.SendCommand($"{_name}.Other", "all-on", TestTimeouts.Short));
    }

    // It only looks, as SignalExitIfRunning: an instance that starts right after it is the first.
    [Fact]
    public void SendCommand_NoneRunning_CreatesNothing()
    {
        Assert.Null(SingleInstance.SendCommand(_name, "all-on", TestTimeouts.Short));

        using var next = SingleInstance.Acquire(_name);
        Assert.True(next.IsPrimary);
    }

    [Fact]
    public async Task SendCommand_NoAnswerInTime_IsNull_AndTheNextGetsItsOwnAnswer()
    {
        using var primary = SingleInstance.Acquire(_name);
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

        var slow = await Task.Run(() => SingleInstance.SendCommand(_name, "slow", TimeSpan.FromMilliseconds(100)), Ct);
        var next = Task.Run(() => SingleInstance.SendCommand(_name, "fast", TestTimeouts.Short), Ct);
        await Task.Delay(TimeSpan.FromMilliseconds(100), Ct);
        release.Set();

        Assert.Null(slow);
        Assert.Equal(2, await next);
    }

    // On a thread of the pool, where an exception would end the process (and this test run).
    [Fact]
    public async Task SendCommand_CallbackThrows_IsNull_AndTheListenerKeepsAnswering()
    {
        using var primary = SingleInstance.Acquire(_name);
        using var listening = primary.ListenForCommand(command => command == "bad" ? throw new IOException("Kapot") : 5);

        var bad = await Task.Run(() => SingleInstance.SendCommand(_name, "bad", TimeSpan.FromMilliseconds(300)), Ct);
        var good = await Task.Run(() => SingleInstance.SendCommand(_name, "good", TestTimeouts.Short), Ct);

        Assert.Null(bad);
        Assert.Equal(5, good);
    }

    [Fact]
    public void SendCommand_TooLong_Throws()
    {
        Assert.Throws<ArgumentException>(() => SingleInstance.SendCommand(_name, new string('x', SingleInstance.MaxCommandBytes + 1), TestTimeouts.Short));
    }

    [Fact]
    public async Task ListenForCommand_AfterListenerIsDisposed_NoneListens()
    {
        using var primary = SingleInstance.Acquire(_name);
        primary.ListenForCommand(_ => 0).Dispose();

        var answer = await Task.Run(() => SingleInstance.SendCommand(_name, "all-on", TimeSpan.FromMilliseconds(200)), Ct);

        Assert.Null(answer);
    }

    [Fact]
    public void ListenForCommand_OnSecondaryInstance_Throws()
    {
        using var first = SingleInstance.Acquire(_name);
        using var second = SingleInstance.Acquire(_name);

        Assert.Throws<InvalidOperationException>(() => second.ListenForCommand(_ => 0));
    }

    [Fact]
    public void ListenForExit_OnSecondaryInstance_Throws()
    {
        using var first = SingleInstance.Acquire(_name);
        using var second = SingleInstance.Acquire(_name);

        Assert.Throws<InvalidOperationException>(() => second.ListenForExit(() => { }));
    }

    [Fact]
    public void ListenForActivation_WhileListening_Throws()
    {
        using var primary = SingleInstance.Acquire(_name);
        using var listener = primary.ListenForActivation(() => { });

        Assert.Throws<InvalidOperationException>(() => primary.ListenForActivation(() => { }));
    }

    [Fact]
    public void ListenForActivation_OnSecondaryInstance_Throws()
    {
        using var first = SingleInstance.Acquire(_name);
        using var second = SingleInstance.Acquire(_name);

        Assert.Throws<InvalidOperationException>(() => second.ListenForActivation(() => { }));
    }
}
