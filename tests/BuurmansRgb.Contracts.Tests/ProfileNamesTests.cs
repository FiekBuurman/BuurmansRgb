namespace BuurmansRgb.Contracts.Tests;

public sealed class ProfileNamesTests
{
    [Theory]
    [InlineData("Gamen", "Gamen")]
    [InlineData("  Nacht ", "Nacht")]
    [InlineData("Werk 2", "Werk 2")]
    public void Normalize_ValidName_ReturnsItTrimmed(string name, string expected) =>
        Assert.Equal(expected, ProfileNames.Normalize(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Twee\nregels")]
    [InlineData("Tab\there")]
    public void Normalize_EmptyOrWithControlCharacter_ReturnsNull(string? name) =>
        Assert.Null(ProfileNames.Normalize(name));

    [Fact]
    public void Normalize_MaxLength_IsValidAndOneMoreIsNot()
    {
        Assert.NotNull(ProfileNames.Normalize(new string('a', ProfileNames.MaxLength)));
        Assert.Null(ProfileNames.Normalize(new string('a', ProfileNames.MaxLength + 1)));
    }

    [Fact]
    public void Comparer_IgnoresCase() => Assert.True(ProfileNames.Comparer.Equals("Gamen", "gAMEN"));
}
