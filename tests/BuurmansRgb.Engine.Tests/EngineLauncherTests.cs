using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

// Which exe "Herstarten" starts again; only the path, no process starts.
public sealed class EngineLauncherTests
{
    private const string BaseDirectory = @"C:\Apps\BuurmansRgb\";

    [Theory]
    [InlineData(@"C:\Apps\BuurmansRgb\BuurmansRgb.Engine.exe")]
    [InlineData(@"D:\Elders\buurmansrgb.engine.EXE")]
    public void EnginePath_TheEngineExe_IsThatExe(string processPath)
    {
        Assert.Equal(processPath, EngineLauncher.EnginePath(processPath, BaseDirectory));
    }

    // `dotnet BuurmansRgb.Engine.dll` runs as dotnet.exe, which would start without the engine.
    [Theory]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe")]
    [InlineData(null)]
    public void EnginePath_AnotherProcess_IsTheExeNextToTheEngine(string? processPath)
    {
        Assert.Equal(@"C:\Apps\BuurmansRgb\BuurmansRgb.Engine.exe", EngineLauncher.EnginePath(processPath, BaseDirectory));
    }
}
