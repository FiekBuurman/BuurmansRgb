namespace BuurmansRgb.Engine.Tests;

internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("BuurmansRgb.Tests.").FullName;

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
