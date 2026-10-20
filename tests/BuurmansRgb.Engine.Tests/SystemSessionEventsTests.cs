using System.Buffers.Binary;
using System.Diagnostics;
using BuurmansRgb.Engine.Power;

namespace BuurmansRgb.Engine.Tests;

public sealed class SystemSessionEventsTests
{
    // The real call on this Windows; whether the session is locked depends on the desktop the tests
    // run on, so this checks that the layout of WTSINFOEXW is read where it is.
    [Fact]
    public void ReadSessionInfo_GivesLevelOneOfThisSession()
    {
        using var process = Process.GetCurrentProcess();

        var info = SystemSessionEvents.ReadSessionInfo();

        Assert.NotNull(info);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(info));
        Assert.Equal(process.SessionId, BinaryPrimitives.ReadInt32LittleEndian(info.AsSpan(8)));
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, false)]
    [InlineData(1, -1, false)]
    [InlineData(2, 0, false)]
    public void IsLockedInfo_ReadsTheLockStateOfLevelOne(int level, int flags, bool locked)
    {
        var info = new byte[24];
        BinaryPrimitives.WriteInt32LittleEndian(info, level);
        BinaryPrimitives.WriteInt32LittleEndian(info.AsSpan(16), flags);

        Assert.Equal(locked, SystemSessionEvents.IsLockedInfo(info));
    }

    [Fact]
    public void IsLockedInfo_TooShort_IsUnlocked()
    {
        Assert.False(SystemSessionEvents.IsLockedInfo(new byte[16]));
    }
}
