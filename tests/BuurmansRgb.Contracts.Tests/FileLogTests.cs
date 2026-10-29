using System.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Contracts.Tests;

public sealed class FileLogTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("BuurmansRgb.Tests.").FullName;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 21, 9, 30, 15, TimeSpan.Zero));

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string LogPath => Path.Combine(_directory, "logs", "engine.log");

    // The settings app opens the log of the engine where the engine writes it.
    [Fact]
    public void PathOf_IsTheLogInTheLogsFolder()
    {
        Assert.Equal(Path.Combine(FileLog.DefaultDirectory, "engine.log"), FileLog.PathOf(FileLog.EngineName));
        Assert.Equal(Path.Combine(FileLog.DefaultDirectory, "ui.log"), FileLog.PathOf(FileLog.UiName));
    }

    [Fact]
    public void TraceEvent_WritesTheTimeTheLevelAndTheMessage()
    {
        using (var log = new FileLog(LogPath, _clock))
        {
            log.TraceEvent(null, "engine", TraceEventType.Error, 0, "Sending colors to OpenRGB failed.");
            log.TraceEvent(null, "engine", TraceEventType.Information, 0, "Connected with {0} devices.", 5);
        }

        var lines = File.ReadAllLines(LogPath);
        var time = _clock.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss.fff");
        Assert.Equal($"{time} Error       Sending colors to OpenRGB failed.", lines[0]);
        Assert.Equal($"{time} Information Connected with 5 devices.", lines[1]);
    }

    // The engine tries OpenRGB every 5 seconds; a day without it must not fill the log.
    [Fact]
    public void TraceEvent_SameMessageAgain_CountsItInsteadOfWritingIt()
    {
        using (var log = new FileLog(LogPath, _clock))
        {
            for (var i = 0; i < 4; i++)
            {
                log.TraceEvent(null, "engine", TraceEventType.Information, 0, "OpenRGB is not reachable.");
            }

            log.TraceEvent(null, "engine", TraceEventType.Information, 0, "Connected to OpenRGB.");
            log.TraceEvent(null, "engine", TraceEventType.Information, 0, "Connected to OpenRGB.");
        }

        var lines = File.ReadAllLines(LogPath);
        Assert.Equal(4, lines.Length);
        Assert.EndsWith("OpenRGB is not reachable.", lines[0]);
        Assert.EndsWith("(the message above came 3 more times)", lines[1]);
        Assert.EndsWith("Connected to OpenRGB.", lines[2]);
        Assert.EndsWith("(the message above came 1 more time)", lines[3]);
    }

    [Fact]
    public void TraceEvent_SameTextAtAnotherLevel_WritesItAgain()
    {
        using (var log = new FileLog(LogPath, _clock))
        {
            log.TraceEvent(null, "engine", TraceEventType.Warning, 0, "Zone length not taken.");
            log.TraceEvent(null, "engine", TraceEventType.Error, 0, "Zone length not taken.");
        }

        Assert.Equal(2, File.ReadAllLines(LogPath).Length);
    }

    [Fact]
    public void New_AppendsToTheLogOfAnEarlierStart()
    {
        using (var log = new FileLog(LogPath, _clock))
        {
            log.WriteLine("first start");
        }

        using (var log = new FileLog(LogPath, _clock))
        {
            log.WriteLine("second start");
        }

        var lines = File.ReadAllLines(LogPath);
        Assert.EndsWith("first start", lines[0]);
        Assert.EndsWith("second start", lines[1]);
    }

    [Fact]
    public void TraceEvent_PastTheMaximumSize_MovesTheLogAsideAndStartsANewOne()
    {
        using (var log = new FileLog(LogPath, _clock, maxBytes: 200))
        {
            for (var i = 0; i < 10; i++)
            {
                log.TraceEvent(null, "engine", TraceEventType.Information, 0, $"Message {i} with some text to fill the log.");
            }

            Assert.True(File.Exists(log.OldPath));
        }

        Assert.True(new FileInfo(LogPath).Length < 400);
        Assert.EndsWith("Message 9 with some text to fill the log.", File.ReadAllLines(LogPath)[^1]);
    }

    [Fact]
    public void New_LogFromEarlierPastTheMaximumSize_MovesItAside()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        File.WriteAllText(LogPath, new string('x', 300));

        using (var log = new FileLog(LogPath, _clock, maxBytes: 200))
        {
            log.WriteLine("after the move");
            Assert.Equal(new string('x', 300), File.ReadAllText(log.OldPath));
        }

        Assert.Single(File.ReadAllLines(LogPath));
    }

    // Shared for reading, so the user can open the log while the engine runs.
    [Fact]
    public void Log_CanBeReadWhileItIsOpen()
    {
        using var log = new FileLog(LogPath, _clock);
        log.WriteLine("while running");

        using var reader = new StreamReader(new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
        Assert.EndsWith("while running", reader.ReadToEnd().TrimEnd());
    }

    [Fact]
    public void New_FolderCannotBeMade_WritesNothingAndDoesNotThrow()
    {
        var blocker = Path.Combine(_directory, "blocker");
        File.WriteAllText(blocker, "a file where the logs folder should be");

        using var log = new FileLog(Path.Combine(blocker, "engine.log"), _clock);
        log.TraceEvent(null, "engine", TraceEventType.Error, 0, "Nowhere to go.");
    }
}
