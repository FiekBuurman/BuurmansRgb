using System.Security;
using BuurmansRgb.Engine.AutoStart;

namespace BuurmansRgb.Engine.Tests;

public sealed class AutoStartExtensionsTests
{
    // Whether the first run turns it on (--first-run, no choice of the user yet) is in EngineHostAutoStartTests.
    [Fact]
    public void EnableOnFirstRun_TurnsAutoStartOn()
    {
        var autoStart = new FakeAutoStart();

        autoStart.EnableOnFirstRun();

        Assert.True(autoStart.IsEnabled);
    }

    [Fact]
    public void EnableOnFirstRun_AlreadyOn_DoesNotWriteAgain()
    {
        var autoStart = new FakeAutoStart { IsEnabled = true };

        autoStart.EnableOnFirstRun();

        Assert.True(autoStart.IsEnabled);
        Assert.Equal(0, autoStart.SetEnabledCount);
    }

    public static TheoryData<Exception> RegistryFailures =>
        [new UnauthorizedAccessException(), new SecurityException(), new IOException()];

    [Theory]
    [MemberData(nameof(RegistryFailures))]
    public void EnableOnFirstRun_RegistryFails_StartsAnyway(Exception failure)
    {
        var autoStart = new FakeAutoStart { SetEnabledException = failure };

        autoStart.EnableOnFirstRun();

        Assert.False(autoStart.IsEnabled);
    }

    [Fact]
    public void EnableOnFirstRun_UnexpectedFailure_IsNotSwallowed()
    {
        var autoStart = new FakeAutoStart { SetEnabledException = new InvalidOperationException() };

        Assert.Throws<InvalidOperationException>(() => autoStart.EnableOnFirstRun());
    }

    [Fact]
    public void FollowMoveOnStart_Moved_IsLogged()
    {
        using var trace = new TraceCapture();
        var autoStart = new FakeAutoStart { FollowMoveResult = "\"C:\\Old\\BuurmansRgb.Engine.exe\"" };

        autoStart.FollowMoveOnStart();

        Assert.Contains(trace.Messages, message => message.Contains("C:\\Old\\BuurmansRgb.Engine.exe", StringComparison.Ordinal));
    }

    // Like a failure on the first run: the engine starts anyway.
    [Theory]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(SecurityException))]
    [InlineData(typeof(IOException))]
    public void FollowMoveOnStart_Fails_DoesNotThrow(Type exceptionType)
    {
        var autoStart = new FakeAutoStart { FollowMoveException = (Exception)Activator.CreateInstance(exceptionType)! };

        autoStart.FollowMoveOnStart();
    }
}
