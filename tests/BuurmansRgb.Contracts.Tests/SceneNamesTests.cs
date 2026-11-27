namespace BuurmansRgb.Contracts.Tests;

public sealed class SceneNamesTests
{
    [Fact]
    public void Comparer_IgnoresCase() => Assert.True(SceneNames.Comparer.Equals("Gamen", "gAMEN"));
}
