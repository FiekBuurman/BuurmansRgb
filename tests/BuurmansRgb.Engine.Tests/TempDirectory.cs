using System.Diagnostics;

namespace BuurmansRgb.Engine.Tests;

internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("BuurmansRgb.Tests.").FullName;

    public string File(string name) => System.IO.Path.Combine(Path, name);

    // Windows may hold on to a file for a moment after the last handle to it closed, such as the exe
    // of a program a test started and stopped, so the delete is tried again for a while. A directory
    // that stays is left in the temp folder, and the test that passed stays green. It gets a warning,
    // which the test exe itself shows, and the same line as output, as dotnet test leaves a warning
    // out: that shows it with --output Detailed (and --fail-warns on makes such a test fail).
    public void Dispose()
    {
        var stopwatch = Stopwatch.StartNew();
        while (Directory.Exists(Path))
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (stopwatch.Elapsed >= TestTimeouts.Short)
                {
                    var message = $"Could not delete {Path}: {exception.Message}";
                    TestContext.Current.AddWarning(message);
                    TestContext.Current.TestOutputHelper?.WriteLine(message);
                    return;
                }

                Thread.Sleep(50);
            }
        }
    }
}
