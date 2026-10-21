using BuurmansRgb.Core;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

// LoadIfChanged: telling a settings file changed by hand from the store's own saves.
public sealed class JsonSettingsStoreChangeTests : IDisposable
{
    private readonly TempDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    private string FilePath => _directory.File("settings.json");

    [Fact]
    public void LoadIfChanged_AfterItsOwnSave_ReturnsNothing()
    {
        var store = new JsonSettingsStore(FilePath);
        store.LoadOrCreate();

        store.Save(new EngineSettings { FramesPerSecond = 12 });

        Assert.Null(store.LoadIfChanged());
    }

    [Fact]
    public void LoadIfChanged_FileAsItWasRead_ReturnsNothing()
    {
        File.WriteAllText(FilePath, """{ "framesPerSecond": 12 }""");
        var store = new JsonSettingsStore(FilePath);
        store.LoadOrCreate();

        Assert.Null(store.LoadIfChanged());
    }

    [Fact]
    public void LoadIfChanged_ChangedByHand_ReturnsTheNewSettingsOnce()
    {
        var store = new JsonSettingsStore(FilePath);
        store.LoadOrCreate();

        File.WriteAllText(FilePath, """{ "framesPerSecond": 12, "color": "#FF8800" }""");

        var changed = store.LoadIfChanged();
        Assert.NotNull(changed);
        Assert.Equal(12, changed.FramesPerSecond);
        Assert.Equal(new RgbColor(255, 136, 0), changed.Color);
        Assert.Null(store.LoadIfChanged());
    }

    // An editor that saves the same content again changes nothing.
    [Fact]
    public void LoadIfChanged_SavedByHandWithTheSameContent_ReturnsNothing()
    {
        var store = new JsonSettingsStore(FilePath);
        store.Save(new EngineSettings { FramesPerSecond = 12 });

        File.WriteAllBytes(FilePath, File.ReadAllBytes(FilePath));

        Assert.Null(store.LoadIfChanged());
    }

    [Fact]
    public void LoadIfChanged_ChangedByHandAfterItsOwnSave_ReturnsTheChange()
    {
        var store = new JsonSettingsStore(FilePath);
        store.Save(new EngineSettings { FramesPerSecond = 12 });

        File.WriteAllText(FilePath, File.ReadAllText(FilePath).Replace("12", "24", StringComparison.Ordinal));

        Assert.Equal(24, store.LoadIfChanged()?.FramesPerSecond);
    }

    // Half written, or a typo: the engine keeps what it has, and tries again on the next change.
    [Theory]
    [InlineData("")]
    [InlineData("""{ "framesPerSecond": 12, "color": """)]
    [InlineData("""{ "framesPerSecond": 12,, }""")]
    public void LoadIfChanged_FileThatDoesNotRead_ReturnsNothingAndKeepsTheFile(string content)
    {
        var store = new JsonSettingsStore(FilePath);
        store.LoadOrCreate();
        using var trace = new TraceCapture();

        File.WriteAllText(FilePath, content);

        Assert.Null(store.LoadIfChanged());
        Assert.Equal(content, File.ReadAllText(FilePath));
        Assert.Contains(trace.Messages, message => message.Contains(FilePath, StringComparison.Ordinal) && message.Contains("does not read", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadIfChanged_FileThatReadsAgain_ReturnsIt()
    {
        var store = new JsonSettingsStore(FilePath);
        store.LoadOrCreate();
        File.WriteAllText(FilePath, """{ "framesPerSecond": 12, "color": """);
        Assert.Null(store.LoadIfChanged());

        File.WriteAllText(FilePath, """{ "framesPerSecond": 12, "color": "#FF8800" }""");

        Assert.Equal(12, store.LoadIfChanged()?.FramesPerSecond);
    }

    // As at the start: the save would otherwise overwrite what the user was typing.
    [Fact]
    public void Save_AfterAChangeThatDidNotRead_MovesTheFileAsideFirst()
    {
        var store = new JsonSettingsStore(FilePath);
        store.LoadOrCreate();
        File.WriteAllText(FilePath, """{ "framesPerSecond": 12,, }""");
        store.LoadIfChanged();

        store.Save(new EngineSettings { FramesPerSecond = 24 });

        Assert.Equal("""{ "framesPerSecond": 12,, }""", File.ReadAllText(store.BrokenFilePath));
        Assert.Equal(24, store.LoadOrCreate().FramesPerSecond);
    }

    [Fact]
    public void Save_AfterAChangeThatReadAgain_LeavesNothingAside()
    {
        var store = new JsonSettingsStore(FilePath);
        store.LoadOrCreate();
        File.WriteAllText(FilePath, """{ "framesPerSecond": 12,, }""");
        store.LoadIfChanged();
        File.WriteAllText(FilePath, """{ "framesPerSecond": 12 }""");
        store.LoadIfChanged();

        store.Save(new EngineSettings { FramesPerSecond = 24 });

        Assert.False(File.Exists(store.BrokenFilePath));
    }

    [Fact]
    public void LoadIfChanged_FileThatIsGone_ReturnsNothingAndTheNextSaveWritesIt()
    {
        var store = new JsonSettingsStore(FilePath);
        store.Save(new EngineSettings { FramesPerSecond = 12 });
        File.Delete(FilePath);

        Assert.Null(store.LoadIfChanged());

        store.Save(new EngineSettings { FramesPerSecond = 12 });
        Assert.Equal(12, store.LoadOrCreate().FramesPerSecond);
    }

    // An editor that still holds the file: its last write is a change of its own.
    [Fact]
    public void LoadIfChanged_FileThatIsHeld_ReturnsNothingUntilItIsFree()
    {
        var store = new JsonSettingsStore(FilePath);
        store.LoadOrCreate();
        File.WriteAllText(FilePath, """{ "framesPerSecond": 12 }""");

        using (new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Null(store.LoadIfChanged());
        }

        Assert.Equal(12, store.LoadIfChanged()?.FramesPerSecond);
    }

    // As at the start: a value that does not read takes its default, the rest is applied.
    [Fact]
    public void LoadIfChanged_ValueOfTheWrongType_TakesItsDefaultAndKeepsTheRest()
    {
        var store = new JsonSettingsStore(FilePath);
        store.LoadOrCreate();

        File.WriteAllText(FilePath, """{ "framesPerSecond": "fast", "brightness": 0.4 }""");

        var changed = store.LoadIfChanged();
        Assert.NotNull(changed);
        Assert.Equal(EngineSettings.CreateDefault().FramesPerSecond, changed.FramesPerSecond);
        Assert.Equal(0.4, changed.Brightness);
    }
}
