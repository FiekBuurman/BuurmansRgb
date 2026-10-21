using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Profiles;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

// Scenes whose program counts only while it is in the foreground (ForegroundOnly), alone and
// together with scenes whose program counts while it runs.
public sealed class ProgramWatcherForegroundTests : IDisposable
{
    private const string Film = "vlc.exe";
    private const string Strategy = "civ6.exe";
    private const string Game = "eldenring.exe";
    private const string Explorer = "explorer.exe";

    private static readonly RgbColor Purple = new(128, 0, 255);
    private static readonly RgbColor White = new(255, 255, 255);
    private static readonly TimeSpan Interval = ProgramWatcher.DefaultPollInterval;

    private readonly InMemoryProfileStore _store = new();
    private readonly LightingController _lighting = new([new RecordingDeviceProvider()]);
    private readonly FakeRunningPrograms _programs = new();
    private readonly FakeForegroundWindow _foreground = new();
    private readonly FakeAwayState _away = new();
    private readonly ClockTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
    private readonly ProfileLibrary _profiles;
    private ProgramWatcher? _watcher;

    public ProgramWatcherForegroundTests()
    {
        _profiles = new ProfileLibrary(_store, _lighting);
        _store.Profiles =
        [
            Profile("Film", Purple, Film, foregroundOnly: true),
            Profile("Nacht", Orange),
            Profile("Strategie", White, Strategy, foregroundOnly: true),
        ];
        ShowColor(Green);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _lighting.Dispose();
    }

    // Without a scene that asks for it, nothing follows the foreground, and a program that runs
    // counts as before, wherever its window is.
    [Fact]
    public void NoForegroundScene_FollowsNothingAndCountsWhileItRuns()
    {
        _store.Profiles = [Profile("Gamen", Red, Game)];
        StartWatcher();
        Assert.False(_foreground.Following);
        Assert.Equal(0, _foreground.FollowCalls);

        _foreground.Bring(Game);
        Assert.Equal(Green, ShownColor());

        _programs.Running.Add(Game);
        _foreground.Bring(Explorer);
        _time.Advance(Interval);
        Assert.Equal(Red, ShownColor());
    }

    [Fact]
    public void BeforeStart_FollowsNothing()
    {
        _watcher = new ProgramWatcher(_profiles, _lighting, _programs, _away, _foreground, _time);

        Assert.Equal(0, _foreground.FollowCalls);
    }

    // Windows tells it; nothing looks in the process list until a program is in front.
    [Fact]
    public void ComesToTheFront_LoadsItsSceneAtOnceWithoutLooking()
    {
        StartWatcher();
        Assert.True(_foreground.Following);
        Assert.False(_time.HasPendingTimer);

        BringToFront(Film);

        Assert.Equal(Purple, ShownColor());
        Assert.Equal("Film", _profiles.ActiveName);
        Assert.Equal(0, _programs.FindCount);
    }

    [Fact]
    public void AnotherProgramComesToTheFront_GoesBackToTheLightingFromBefore()
    {
        StartWatcher();
        _profiles.Load("Nacht");
        BringToFront(Film);

        _foreground.Bring(Explorer);

        Assert.Equal(Orange, ShownColor());
        Assert.Equal("Nacht", _profiles.ActiveName);
    }

    // Running behind another window is not enough.
    [Fact]
    public void RunsButNotInFront_LoadsNothing()
    {
        StartWatcher();
        _programs.Running.Add(Film);
        _foreground.Bring(Explorer);

        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(Green, ShownColor());
        Assert.Equal(0, _programs.FindCount);
    }

    // Alt+Tab from one to the other shows the second scene straight away, without the lighting
    // from before in between; going to a third program then brings that lighting back.
    [Fact]
    public void FromOneForegroundSceneToAnother_GoesStraightThere()
    {
        StartWatcher();
        BringToFront(Film);
        var shown = new List<RgbColor>();
        _lighting.LightingChanged += (_, _) => shown.Add(ShownColor());

        BringToFront(Strategy);
        Assert.Equal([White], shown);
        Assert.Equal("Strategie", _profiles.ActiveName);

        BringToFront(Film);
        Assert.Equal([White, Purple], shown);

        _foreground.Bring(Explorer);
        Assert.Equal([White, Purple, Green], shown);
    }

    // Another window of the same program changes nothing.
    [Fact]
    public void SameProgramAgain_LoadsOnce()
    {
        StartWatcher();
        BringToFront(Film);
        var changes = 0;
        _lighting.LightingChanged += (_, _) => changes++;

        _foreground.Bring("VLC.exe");

        Assert.Equal(0, changes);
    }

    // When it closes, Windows mostly brings another window to the front; when it does not, the
    // look while it is in front sees it stop. Then there is nothing left to look for.
    [Fact]
    public void StopsWhileInFront_GoesBackAtTheNextLook()
    {
        StartWatcher();
        BringToFront(Film);
        Assert.True(_time.HasPendingTimer);
        _time.Advance(Interval);
        Assert.Equal(Purple, ShownColor());

        _programs.Running.Remove(Film);
        _time.Advance(Interval);

        Assert.Equal(Green, ShownColor());
        Assert.False(_time.HasPendingTimer);
    }

    // A program whose name the process list cannot read yet does not count as stopped.
    [Fact]
    public void InFrontButNotSeenRunningYet_KeepsItsScene()
    {
        StartWatcher();
        _foreground.Bring(Film);

        _time.Advance(Interval);
        Assert.Equal(Purple, ShownColor());

        _programs.Running.Add(Film);
        _time.Advance(Interval);
        _programs.Running.Remove(Film);
        _time.Advance(Interval);
        Assert.Equal(Green, ShownColor());
    }

    [Fact]
    public void ChangedWhileInFront_KeepsThatAfterSwitchingAway()
    {
        StartWatcher();
        BringToFront(Film);
        ShowColor(Blue);

        _foreground.Bring(Explorer);

        Assert.Equal(Blue, ShownColor());
    }

    // Each time it comes to the front counts as a start, on top of what shows then.
    [Fact]
    public void BackToTheFrontAfterAChange_LoadsAgainAndGoesBackToThatChange()
    {
        StartWatcher();
        BringToFront(Film);
        _foreground.Bring(Explorer);
        ShowColor(Blue);

        BringToFront(Film);
        Assert.Equal(Purple, ShownColor());

        _foreground.Bring(Explorer);
        Assert.Equal(Blue, ShownColor());
    }

    // A game that counts while it runs shows its scene again once the foreground scene is left,
    // also when the game's window is what comes to the front.
    [Fact]
    public void ForegroundSceneOverARunningGame_GoesBackToTheGame()
    {
        _store.Profiles = [.. _store.Profiles, Profile("Gamen", Red, Game)];
        StartWatcher();
        _programs.Running.Add(Game);
        _time.Advance(Interval);
        Assert.Equal(Red, ShownColor());

        BringToFront(Film);
        Assert.Equal(Purple, ShownColor());

        _foreground.Bring(Game);
        Assert.Equal(Red, ShownColor());
        Assert.Equal("Gamen", _profiles.ActiveName);

        _programs.Running.Remove(Game);
        _time.Advance(Interval);
        Assert.Equal(Green, ShownColor());
    }

    // The last one that started shows, as with two games: a game that starts behind the film takes
    // over, and leaving the film does not take the game away.
    [Fact]
    public void GameStartsWhileAForegroundSceneShows_TheGameShowsUntilItStops()
    {
        _store.Profiles = [.. _store.Profiles, Profile("Gamen", Red, Game)];
        StartWatcher();
        BringToFront(Film);

        _programs.Running.Add(Game);
        _time.Advance(Interval);
        Assert.Equal(Red, ShownColor());

        _foreground.Bring(Explorer);
        Assert.Equal(Red, ShownColor());

        _programs.Running.Remove(Game);
        _time.Advance(Interval);
        Assert.Equal(Green, ShownColor());
    }

    // Nothing in front counts on the lock screen; right after, what is in front then counts.
    [Fact]
    public void WhileAway_KeepsTheSceneAndRightAfterFollowsWhatIsInFront()
    {
        StartWatcher();
        BringToFront(Film);

        _away.Raise(PauseReasons.Locked);
        _foreground.Bring(Explorer);
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(Purple, ShownColor());

        _away.Raise(PauseReasons.None);
        _time.Advance(TimeSpan.Zero);
        Assert.Equal(Green, ShownColor());
    }

    [Fact]
    public void CameToTheFrontWhileAway_LoadsRightAfter()
    {
        StartWatcher();
        _away.Raise(PauseReasons.Asleep);
        BringToFront(Strategy);
        Assert.Equal(Green, ShownColor());

        _away.Raise(PauseReasons.None);
        _time.Advance(TimeSpan.Zero);

        Assert.Equal(White, ShownColor());
    }

    // The choice switched on or off is like another program: what showed stays, and it counts
    // again from its next start or the next time it comes to the front.
    [Fact]
    public void ForegroundOnlySwitchedOnWhileTheGameShows_KeepsTheLightingAndWaitsForTheFront()
    {
        _store.Profiles = [Profile("Gamen", Red, Game), Profile("Nacht", Orange)];
        StartWatcher();
        _programs.Running.Add(Game);
        _time.Advance(Interval);
        Assert.False(_foreground.Following);

        _profiles.SetAutomation("Gamen", new ProfileAutomation(Program: Game, ForegroundOnly: true));
        Assert.True(_foreground.Following);
        _time.Advance(Interval);
        _programs.Running.Remove(Game);
        _time.Advance(Interval);
        Assert.Equal(Red, ShownColor());

        ShowColor(Green);
        BringToFront(Game);
        Assert.Equal(Red, ShownColor());
        _foreground.Bring(Explorer);
        Assert.Equal(Green, ShownColor());
    }

    [Fact]
    public void ForegroundOnlySwitchedOffWhileInFront_KeepsTheLightingAndStopsFollowing()
    {
        _store.Profiles = [Profile("Film", Purple, Film, foregroundOnly: true)];
        StartWatcher();
        BringToFront(Film);

        _profiles.SetAutomation("Film", new ProfileAutomation(Program: Film));
        Assert.False(_foreground.Following);
        _foreground.Bring(Explorer);
        _programs.Running.Remove(Film);
        _time.Advance(Interval);

        Assert.Equal(Purple, ShownColor());
    }

    [Fact]
    public void LastForegroundSceneGone_StopsFollowing()
    {
        StartWatcher();

        _profiles.Delete("Film");
        Assert.True(_foreground.Following);
        _profiles.SetAutomation("Strategie", ProfileAutomation.None);

        Assert.False(_foreground.Following);
        Assert.Equal(2, _foreground.FollowCalls);
    }

    // So the engine saves the lighting from before, as when a game runs.
    [Fact]
    public void Dispose_WhileInFront_PutsTheLightingFromBeforeBackAndStopsFollowing()
    {
        StartWatcher();
        BringToFront(Film);

        _watcher!.Dispose();

        Assert.Equal(Green, ShownColor());
        Assert.False(_foreground.Following);
        Assert.Equal(0, _foreground.Listeners);
        Assert.False(_time.HasPendingTimer);
    }

    // A time that comes while it is in front waits until another program comes to the front.
    [Fact]
    public void HoldWhileInFront_ShowsTheTimeSceneOnceSwitchedAway()
    {
        StartWatcher();
        BringToFront(Film);

        Assert.True(_watcher!.HoldUntilProgramsStop("Nacht"));
        Assert.Equal(Purple, ShownColor());

        _foreground.Bring(Explorer);
        Assert.Equal(Orange, ShownColor());
        Assert.Equal("Nacht", _profiles.ActiveName);
    }

    private static Profile Profile(string name, RgbColor color, string? program = null, bool foregroundOnly = false) =>
        new(name, Uniform(StaticLighting(color))) { Automation = new ProfileAutomation(Program: program, ForegroundOnly: foregroundOnly) };

    private void StartWatcher()
    {
        _watcher = new ProgramWatcher(_profiles, _lighting, _programs, _away, _foreground, _time);
        _watcher.Start();
    }

    // A window of program comes to the front; a program in front runs.
    private void BringToFront(string program)
    {
        _programs.Running.Add(program);
        _foreground.Bring(program);
    }

    private RgbColor ShownColor() => _lighting.GetLighting(null).Color;

    private void ShowColor(RgbColor color) => _lighting.SetLighting(null, lighting => lighting with { Color = color });
}
