namespace BuurmansRgb.Engine.Tests;

public sealed class SingleInstanceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

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

        await activated.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
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

        await activated.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ListenForActivation_OnSecondaryInstance_Throws()
    {
        using var first = SingleInstance.Acquire(_name);
        using var second = SingleInstance.Acquire(_name);

        Assert.Throws<InvalidOperationException>(() => second.ListenForActivation(() => { }));
    }
}
