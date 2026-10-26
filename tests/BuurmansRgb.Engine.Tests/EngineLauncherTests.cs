using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

// Which exe "Herstarten" starts again; only the path, no process starts.
public sealed class EngineLauncherTests
{
    private const string BaseDirectory = @"C:\Apps\BuurmansRgb\";

    [Theory]
    [InlineData(@"C:\Apps\BuurmansRgb\BuurmansRgb.Engine.exe")]
    [InlineData(@"D:\Elders\buurmansrgb.engine.EXE")]
    [InlineData(@"D:\Elders\RgbEngine.exe")]
    public void EnginePath_TheEngineExe_IsThatExe(string processPath)
    {
        Assert.Equal(processPath, EngineLauncher.EnginePath(processPath, BaseDirectory));
    }

    // `dotnet BuurmansRgb.Engine.dll` runs as dotnet.exe, which would start without the engine.
    // Any other exe is the engine, also when renamed.
    [Theory]
    [InlineData(@"C:\Program Files\dotnet\dotnet.exe")]
    [InlineData(@"C:\Program Files\dotnet\DOTNET.EXE")]
    [InlineData("/usr/share/dotnet/dotnet")]
    [InlineData(null)]
    public void EnginePath_DotnetOrUnknown_IsTheExeNextToTheEngine(string? processPath)
    {
        Assert.Equal(@"C:\Apps\BuurmansRgb\BuurmansRgb.Engine.exe", EngineLauncher.EnginePath(processPath, BaseDirectory));
    }
}
