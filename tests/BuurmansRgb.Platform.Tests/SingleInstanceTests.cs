namespace BuurmansRgb.Platform.Tests;

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

    // An engine or settings app of another version finds this one only by these names (a new
    // installer over a running engine, "Herstarten"), so they never change.
    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void Acquire_KernelObjectNames_AreFixed()
    {
        using var primary = SingleInstance.Acquire(_name, acceptsCommands: true);

        Assert.True(Mutex.TryOpenExisting($@"Local\{_name}", out var mutex));
        Assert.True(EventWaitHandle.TryOpenExisting($@"Local\{_name}.Activate", out var activation));
        Assert.True(EventWaitHandle.TryOpenExisting($@"Local\{_name}.Exit", out var exit));
        Assert.True(EventWaitHandle.TryOpenExisting($@"Local\{_name}.Command", out var command));
        mutex.Dispose();
        activation.Dispose();
        exit.Dispose();
        command.Dispose();
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

        // No signal exists for "still waiting": this pause can only let a broken wait stay green,
        // never turn a good one red.
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

    [Fact]
    public void ListenForCommand_InstanceThatTakesNoCommands_Throws()
    {
        using var settingsApp = SingleInstance.Acquire(_name);

        Assert.True(settingsApp.IsPrimary);
        Assert.Throws<InvalidOperationException>(() => settingsApp.ListenForCommand(_ => 0));
    }

    [Fact]
    public void ListenForCommand_OnSecondaryInstance_Throws()
    {
        using var first = SingleInstance.Acquire(_name, acceptsCommands: true);
        using var second = SingleInstance.Acquire(_name, acceptsCommands: true);

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
