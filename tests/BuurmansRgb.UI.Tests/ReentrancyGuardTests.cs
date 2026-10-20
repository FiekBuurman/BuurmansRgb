using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class ReentrancyGuardTests
{
    private readonly ReentrancyGuard _guard = new();

    [Fact]
    public void Run_IsActiveOnlyDuringTheAction()
    {
        var during = false;

        _guard.Run(() => during = _guard.IsActive);

        Assert.True(during);
        Assert.False(_guard.IsActive);
    }

    [Fact]
    public void Run_Nested_StaysActiveUntilTheOuterRunEnds()
    {
        var afterInner = false;

        _guard.Run(() =>
        {
            _guard.Run(() => { });
            afterInner = _guard.IsActive;
        });

        Assert.True(afterInner);
        Assert.False(_guard.IsActive);
    }

    [Fact]
    public void Run_ActionThrows_EndsTheRun()
    {
        Assert.Throws<InvalidOperationException>(() => _guard.Run(() => throw new InvalidOperationException()));

        Assert.False(_guard.IsActive);
    }
}
