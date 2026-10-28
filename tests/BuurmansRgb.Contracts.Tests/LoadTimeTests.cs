namespace BuurmansRgb.Contracts.Tests;

public sealed class LoadTimeTests
{
    [Theory]
    [InlineData("23:00", 23, 0)]
    [InlineData(" 7:30 ", 7, 30)]
    [InlineData("07:30", 7, 30)]
    [InlineData("21.30", 21, 30)]
    [InlineData("07.30", 7, 30)]
    [InlineData("0:00", 0, 0)]
    public void TryParseAt_Time_ReadsIt(string text, int hour, int minute)
    {
        Assert.True(LoadTime.TryParseAt(text, out var at));
        Assert.Equal(new TimeOnly(hour, minute), at);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("25:00")]
    [InlineData("23:60")]
    [InlineData("23:0")]
    [InlineData("9:5")]
    [InlineData("9u")]
    [InlineData("23:00:00")]
    [InlineData("half elf")]
    public void TryParseAt_NoTime_ReadsNothing(string? text) =>
        Assert.False(LoadTime.TryParseAt(text, out _));
}
