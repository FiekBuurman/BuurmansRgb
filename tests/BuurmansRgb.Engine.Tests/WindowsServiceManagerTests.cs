using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

// Only looking up, as a normal user may: no test stops or starts a real service or driver.
public sealed class WindowsServiceManagerTests
{
    private readonly WindowsServiceManager _services = new();

    [Fact]
    public void Exists_UnknownService_IsFalse()
    {
        Assert.False(_services.Exists($"BuurmansRgb.Tests.{Guid.NewGuid():N}"));
    }

    [Fact]
    public void FindDriver_UnknownText_IsNull()
    {
        Assert.Null(_services.FindDriver($"BuurmansRgb.Tests.{Guid.NewGuid():N}"));
    }

    // Every Windows has kernel drivers; a name read from the list is one that exists.
    [Fact]
    public void FindDriver_AnyDriver_GivesANameThatExists()
    {
        var name = _services.FindDriver(string.Empty);

        Assert.False(string.IsNullOrEmpty(name));
        Assert.True(_services.Exists(name));
    }
}
