namespace BuurmansRgb.Contracts.Tests;

public sealed class ProfileNamesTests
{
    [Fact]
    public void Comparer_IgnoresCase() => Assert.True(ProfileNames.Comparer.Equals("Gamen", "gAMEN"));
}
