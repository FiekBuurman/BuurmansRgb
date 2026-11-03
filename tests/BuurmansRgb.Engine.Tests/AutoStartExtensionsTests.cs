using System.Security;
using BuurmansRgb.Engine.AutoStart;

namespace BuurmansRgb.Engine.Tests;

public sealed class AutoStartExtensionsTests
{
    // Turning it on at the first run is in AutoStartSwitchTests (EnableOnFirstRun) and
    // EngineHostAutoStartTests (whether it should).
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
