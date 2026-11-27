namespace BuurmansRgb.Contracts.Tests;

public sealed class DisplayNamesTests
{
    private const int MaxLength = 10;

    [Theory]
    [InlineData("Gamen", "Gamen")]
    [InlineData("  Nacht ", "Nacht")]
    [InlineData("Werk 2", "Werk 2")]
    public void Normalize_ValidName_ReturnsItTrimmed(string name, string expected) =>
        Assert.Equal(expected, DisplayNames.Normalize(name, MaxLength));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Twee\nregels")]
    [InlineData("Tab\there")]
    public void Normalize_EmptyOrWithControlCharacter_ReturnsNull(string? name) =>
        Assert.Null(DisplayNames.Normalize(name, MaxLength));

    [Fact]
    public void Normalize_MaxLength_IsValidAndOneMoreIsNot()
    {
        Assert.NotNull(DisplayNames.Normalize(new string('a', MaxLength), MaxLength));
        Assert.Null(DisplayNames.Normalize(new string('a', MaxLength + 1), MaxLength));
    }

    [Fact]
    public void Normalize_SpacesDoNotCountTowardsTheLength() =>
        Assert.Equal(new string('a', MaxLength), DisplayNames.Normalize($"  {new string('a', MaxLength)}  ", MaxLength));
}
