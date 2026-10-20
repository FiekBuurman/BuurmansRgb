namespace BuurmansRgb.Contracts.Tests;

public sealed class ProgramNamesTests
{
    [Theory]
    [InlineData("eldenring.exe", "eldenring.exe")]
    [InlineData("  EldenRing.EXE ", "EldenRing.EXE")]
    [InlineData("eldenring", "eldenring.exe")]
    [InlineData(@"C:\Games\ELDEN RING\Game\eldenring.exe", "eldenring.exe")]
    [InlineData("\"C:/Games/eldenring.exe\"", "eldenring.exe")]
    [InlineData("Game-Win64-Shipping.exe", "Game-Win64-Shipping.exe")]
    public void Normalize_ValidName_ReturnsTheFileNameWithExe(string program, string expected) =>
        Assert.Equal(expected, ProgramNames.Normalize(program));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".exe")]
    [InlineData(@"C:\Games\")]
    [InlineData("game?.exe")]
    [InlineData("game:2.exe")]
    [InlineData("twee\nregels.exe")]
    public void Normalize_EmptyOrNotAFileName_ReturnsNull(string? program) =>
        Assert.Null(ProgramNames.Normalize(program));

    [Fact]
    public void Normalize_MaxLength_IsValidAndOneMoreIsNot()
    {
        Assert.NotNull(ProgramNames.Normalize(new string('a', ProgramNames.MaxLength - 4) + ".exe"));
        Assert.Null(ProgramNames.Normalize(new string('a', ProgramNames.MaxLength - 3) + ".exe"));
    }

    [Fact]
    public void Comparer_IgnoresCase() => Assert.True(ProgramNames.Comparer.Equals("eldenring.exe", "EldenRing.EXE"));

    [Fact]
    public void ProfileAutomation_Normalize_DropsSecondsAndNormalizesTheProgram() =>
        Assert.Equal(
            new ProfileAutomation(new TimeOnly(23, 0), "eldenring.exe"),
            new ProfileAutomation(new TimeOnly(23, 0, 59), " eldenring ").Normalize());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ProfileAutomation_Normalize_EmptyProgram_IsOff(string? program) =>
        Assert.Null(new ProfileAutomation(Program: program).Normalize().Program);

    [Fact]
    public void ProfileAutomation_Normalize_InvalidProgram_Throws() =>
        Assert.Throws<ArgumentException>(() => new ProfileAutomation(Program: "game?.exe").Normalize());
}
