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
