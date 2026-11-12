using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

// ProfileTimer and ProgramWatcher together, as the engine wires them: a time that comes while a
// game runs.
public sealed class ProfileTriggersTests : IDisposable
{
    private const string Game = "eldenring.exe";
    private const string Film = "vlc.exe";

    // 1 June 2026 22:00 in Amsterdam: 23:00 is an hour away.
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 20, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = ProgramWatcher.DefaultPollInterval;
    private static readonly TimeSpan UntilElevenPm = TimeSpan.FromHours(1);
    private static readonly TimeSpan UntilSevenAm = TimeSpan.FromHours(9);
    private static readonly LoadTime ElevenPm = new(new TimeOnly(23, 0));
    private static readonly LoadTime SevenAm = new(new TimeOnly(7, 0));

    private readonly InMemoryProfileStore _store = new();
    private readonly LightingController _lighting = new([new RecordingDeviceProvider()]);
    private readonly FakeRunningPrograms _programs = new();
    private readonly FakeAwayState _away = new();
    private readonly FakeForegroundWindow _foreground = new();
    private readonly ClockTimeProvider _time = new(Start, DailyTimeTests.Amsterdam);
    private readonly ProfileLibrary _profiles;
    private readonly ProgramWatcher _watcher;
    private readonly ProfileTimer _timer;

    public ProfileTriggersTests()
    {
        _store.Profiles =
        [
            new Profile("Gamen", Uniform(StaticLighting(Red))) { Automation = new ProfileAutomation(Program: Game) },
            new Profile("Film", Uniform(StaticLighting(Blue))) { Automation = new ProfileAutomation(Program: Film, ForegroundOnly: true) },
            new Profile("Nacht", Uniform(StaticLighting(Orange))) { Automation = new ProfileAutomation(new TimeOnly(23, 0)) },
            new Profile("Alles uit", StartScenes.AllOff) { Role = ProfileRole.AllOff },
        ];
        _profiles = new ProfileLibrary(_store, _lighting);
        _watcher = new ProgramWatcher(_profiles, _lighting, _programs, _away, _foreground, _time);
        _timer = new ProfileTimer(_profiles, new FakeClockEvents(), _time, _watcher.HoldUntilProgramsStop);
        ShowColor(Green);
        _timer.Start();
        _watcher.Start();
    }

    public void Dispose()
    {
        _timer.Dispose();
        _watcher.Dispose();
        _lighting.Dispose();
    }

    [Fact]
    public void TimeWhileAGameRuns_KeepsTheGameAndShowsTheTimeAfterIt()
    {
        StartGame();

        _time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(Red, ShownColor());

        StopGame();
        Assert.Equal(Orange, ShownColor());
    }

    [Fact]
    public void TimeWhileAGameRuns_ItsSceneIsActiveOnceTheGameStops()
    {
        StartGame();

        _time.Advance(TimeSpan.FromHours(1));
        Assert.Equal("Gamen", _profiles.ActiveName);

        StopGame();
        Assert.Equal("Nacht", _profiles.ActiveName);
    }

    [Fact]
    public void TimeWhileAGameRunsAndTheEngineStops_SavesTheTimeProfile()
    {
        StartGame();
        _time.Advance(TimeSpan.FromHours(1));

        _timer.Dispose();
        _watcher.Dispose();

        Assert.Equal(Orange, ShownColor());
    }

    // The user changed the lighting during the game, so the game no longer holds it.
    [Fact]
    public void TimeWhileAChangedGameRuns_ShowsTheTimeRightAway()
    {
        StartGame();
        ShowColor(Blue);

        _time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(Orange, ShownColor());
    }

    // The engine does not look for programs on the lock screen, but it does once when the time
    // comes, so a game that stopped there does not hold the time until unlocking.
    [Fact]
    public void TimeWhileLockedAfterTheGameStopped_ShowsRightAway()
    {
        StartGame();
        _away.Raise(PauseReasons.Locked);
        _programs.Running.Remove(Game);

        _time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(Orange, ShownColor());
        Assert.Equal("Nacht", _profiles.ActiveName);
    }

    [Fact]
    public void TimeWhileLockedDuringTheGame_ShowsTheTimeAfterIt()
    {
        StartGame();
        _away.Raise(PauseReasons.Locked);

        _time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(Red, ShownColor());

        _away.Raise(PauseReasons.None);
        StopGame();
        Assert.Equal(Orange, ShownColor());
    }

    // A scene in the foreground holds a time as a game does, until another program comes to the front.
    [Fact]
    public void TimeWhileAForegroundSceneShows_ShowsTheTimeOnceSwitchedAway()
    {
        _programs.Running.Add(Film);
        _foreground.Bring(Film);
        Assert.Equal(Blue, ShownColor());

        _time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(Blue, ShownColor());

        _foreground.Bring("explorer.exe");
        Assert.Equal(Orange, ShownColor());
        Assert.Equal("Nacht", _profiles.ActiveName);
    }

    // The time comes on top of the game; the film in front and back changes that.
    [Fact]
    public void TimeDuringAGame_ThenAForegroundScene_EndsWithTheTimeScene()
    {
        StartGame();
        _time.Advance(TimeSpan.FromHours(1));

        _programs.Running.Add(Film);
        _foreground.Bring(Film);
        Assert.Equal(Blue, ShownColor());
        _foreground.Bring(Game);
        Assert.Equal(Red, ShownColor());

        StopGame();
        Assert.Equal(Orange, ShownColor());
    }

    [Fact]
    public void TimeWithoutAGame_ShowsRightAway()
    {
        _time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(Orange, ShownColor());
    }

    // Uit om 23:00 on the scene "Alles uit" waits for the game; Aan om at 07:00 comes during the
    // game; once it stops, the lighting of the evening comes back on, not every device off.
    [Fact]
    public void UitOmAndAanOmDuringAGame_TheEveningLightingComesBackOnOnceItStops()
    {
        _profiles.SetAutomation("Nacht", new ProfileAutomation());
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm], [SevenAm]));
        Assert.Equal("Alles uit", _profiles.GetWholeHouseTimes().OffScene);
        StartGame();

        _time.Advance(UntilElevenPm);
        Assert.Equal(Red, ShownColor());
        _time.Advance(UntilSevenAm - UntilElevenPm);
        Assert.Equal(Red, ShownColor());

        StopGame();
        Assert.Equal(Green, ShownColor());
        Assert.True(_lighting.State.Global.On);
        Assert.Null(_profiles.ActiveName);
    }

    // The same with the internal Uit om, without a scene "Alles uit".
    [Fact]
    public void InternalUitOmAndAanOmDuringAGame_TheEveningLightingComesBackOnOnceItStops()
    {
        _profiles.Delete("Alles uit");
        _profiles.SetAutomation("Nacht", new ProfileAutomation());
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm], [SevenAm]));
        Assert.Null(_profiles.GetWholeHouseTimes().OffScene);
        StartGame();

        _time.Advance(UntilElevenPm);
        Assert.Equal(Red, ShownColor());
        _time.Advance(UntilSevenAm - UntilElevenPm);
        Assert.Equal(Red, ShownColor());

        StopGame();
        Assert.Equal(Green, ShownColor());
        Assert.True(_lighting.State.Global.On);
        Assert.Null(_profiles.ActiveName);
    }

    // Without Aan om, the internal Uit om held during the game switches every device off once it stops.
    [Fact]
    public void InternalUitOmDuringAGame_EveryDeviceGoesOffOnceItStops()
    {
        _profiles.Delete("Alles uit");
        _profiles.SetAutomation("Nacht", new ProfileAutomation());
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm]));
        StartGame();

        _time.Advance(UntilElevenPm);
        Assert.True(_lighting.State.IsAnyOn);

        StopGame();
        Assert.False(_lighting.State.IsAnyOn);
        Assert.Equal(Green, ShownColor());
    }

    private void StartGame()
    {
        _programs.Running.Add(Game);
        _time.Advance(Interval);
        Assert.Equal(Red, ShownColor());
    }

    private void StopGame()
    {
        _programs.Running.Remove(Game);
        _time.Advance(Interval);
    }

    private RgbColor ShownColor() => _lighting.GetLighting(null).Color;

    private void ShowColor(RgbColor color) => _lighting.SetLighting(null, lighting => lighting with { Color = color });
}
