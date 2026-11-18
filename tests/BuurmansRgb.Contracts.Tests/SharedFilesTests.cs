namespace BuurmansRgb.Contracts.Tests;

public sealed class SharedFilesTests
{
    [Theory]
    [InlineData("Gamen", false)]
    [InlineData("Film & TV", false)]
    [InlineData("C:", true)]
    [InlineData(@"..\Gamen", true)]
    [InlineData("a/b", true)]
    public void IsPathLike_WithACharacterOfAPath(string name, bool pathLike) => Assert.Equal(pathLike, SharedFiles.IsPathLike(name));

    [Fact]
    public void ForSharing_PutsADashForTheCharactersOfAPath()
    {
        Assert.Equal("Film-TV", SharedFiles.ForSharing("Film/TV"));
        Assert.Equal("a-b-c", SharedFiles.ForSharing(@"a\b:c"));
        Assert.Equal("Gamen", SharedFiles.ForSharing("Gamen"));
    }

    [Fact]
    public void FreeName_TheNameWhenFree_ElseTheFirstNumber()
    {
        Assert.Equal("Gamen", SharedFiles.FreeName("Gamen", 40, _ => false));
        Assert.Equal("Gamen (2)", SharedFiles.FreeName("Gamen", 40, name => name == "Gamen"));
        Assert.Equal("Gamen (3)", SharedFiles.FreeName("Gamen", 40, name => name is "Gamen" or "Gamen (2)"));
    }

    // The number fits within the longest name; the end of the name makes way, without a space
    // before the number twice.
    [Fact]
    public void FreeName_OfALongName_CutsTheEndOff()
    {
        var name = new string('a', 36) + " bcd";

        var free = SharedFiles.FreeName(name, 40, other => other == name);

        Assert.Equal(new string('a', 36) + " (2)", free);
        Assert.True(free.Length <= 40);
    }
}
