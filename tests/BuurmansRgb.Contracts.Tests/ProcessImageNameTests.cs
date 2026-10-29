namespace BuurmansRgb.Contracts.Tests;

// Only this own process is read: what else runs differs per pc. The engine's tests of ProcessList,
// SystemForegroundWindow and SystemScreenSaverEvents use it too.
public sealed class ProcessImageNameTests
{
    private readonly ProcessImageName _image = new();

    [Fact]
    public void TryRead_OwnProcess_GivesTheExeName()
    {
        Assert.True(_image.TryRead((uint)Environment.ProcessId, out var name));

        Assert.Equal(Path.GetFileName(Environment.ProcessPath), name, ignoreCase: true);
    }

    [Fact]
    public void ReadPath_OwnProcess_GivesTheWholePath()
    {
        Assert.Equal(Environment.ProcessPath, _image.ReadPath((uint)Environment.ProcessId), ignoreCase: true);
    }

    // Windows opens the idle process for no one (ERROR_INVALID_PARAMETER), which is not the access
    // denied that never passes.
    [Fact]
    public void TheIdleProcess_HasNoName()
    {
        Assert.False(_image.TryRead(0, out var name));
        Assert.Null(name);
        Assert.Null(_image.ReadPath(0));
    }
}
