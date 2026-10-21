using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Profiles;
using Microsoft.Win32;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

public sealed class ProgramWatcherTests : IDisposable
{
    private const string EldenRing = "eldenring.exe";
    private const string Forza = "forza.exe";

    private static readonly TimeSpan Interval = ProgramWatcher.DefaultPollInterval;

    private readonly InMemoryProfileStore _store = new();
    private readonly LightingController _lighting = new([new RecordingDeviceProvider()]);
    private readonly FakeRunningPrograms _programs = new();
    private readonly FakePowerEvents _power = new();
    private readonly FakeSessionEvents _session = new();
    private readonly ClockTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
    private readonly ProfileLibrary _profiles;
    private readonly AutoPause _autoPause;
    private ProgramWatcher? _watcher;

    public ProgramWatcherTests()
    {
        _profiles = new ProfileLibrary(_store, _lighting);
        _autoPause = new AutoPause(_power, _session, _lighting.SetPaused);
        _store.Profiles =
        [
            Profile("Gamen", Red, EldenRing),
            Profile("Nacht", Orange),
            Profile("Race", Blue, Forza),
        ];
        ShowColor(Green);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _autoPause.Dispose();
        _lighting.Dispose();
    }

    [Fact]
    public void NoProfileWithAProgram_NoTimerRunsAndNothingIsLookedUp()
    {
        _store.Profiles = [Profile("Nacht", Orange)];

        StartWatcher();
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.False(_time.HasPendingTimer);
        Assert.Equal(0, _programs.FindCount);
    }

    // Nobody starts a game on the lock screen or while the pc sleeps, whether the effects pause
    // then (pauseOnLockAndSleep) or not.
    [Theory]
    [InlineData("lock", true)]
    [InlineData("lock", false)]
    [InlineData("sleep", true)]
    [InlineData("sleep", false)]
    [InlineData("disconnect", true)]
    [InlineData("disconnect", false)]
    public void WhileAway_LooksForNothing_AndRightAfterLooksAtOnce(string away, bool pauseOnLockAndSleep)
    {
        _autoPause.Enabled = pauseOnLockAndSleep;
        StartWatcher();
        var looks = _programs.FindCount;

        GoAway(away);
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(looks, _programs.FindCount);
        Assert.False(_time.HasPendingTimer);

        _programs.Running.Add(EldenRing);
        ComeBack(away);
        _time.Advance(TimeSpan.Zero);

        Assert.Equal(looks + 1, _programs.FindCount);
        Assert.Equal(Red, ShownColor());
        Assert.True(_time.HasPendingTimer);
    }

    // The game's lighting stays on while Windows is locked; the first look after unlocking sees
    // that it stopped in the meantime and goes back.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProgramStopsWhileLocked_LightingStaysUntilUnlocked_ThenGoesBack(bool pauseOnLockAndSleep)
    {
        _autoPause.Enabled = pauseOnLockAndSleep;
        StartWatcher();
        StartProgram(EldenRing);

        _session.Raise(SessionSwitchReason.SessionLock);
        _programs.Running.Remove(EldenRing);
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(Red, ShownColor());
        Assert.Equal("Gamen", _profiles.ActiveName);

        _session.Raise(SessionSwitchReason.SessionUnlock);
        _time.Advance(TimeSpan.Zero);

        Assert.Equal(Green, ShownColor());
    }

    // An engine that starts late after sign-in may start on the lock screen.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StartedWhileLocked_LooksOnlyAfterUnlocking(bool pauseOnLockAndSleep)
    {
        _session.IsLocked = true;
        using var autoPause = new AutoPause(_power, _session, _lighting.SetPaused) { Enabled = pauseOnLockAndSleep };
        _programs.Running.Add(EldenRing);

        StartWatcher(autoPause);
        Assert.Equal(0, _programs.FindCount);
        Assert.Equal(Green, ShownColor());

        _session.Raise(SessionSwitchReason.SessionUnlock);
        _time.Advance(TimeSpan.Zero);

        Assert.Equal(1, _programs.FindCount);
        Assert.Equal(Red, ShownColor());
    }

    // Turning the pause on or off while locked changes nothing about looking.
    [Fact]
    public void PauseOnLockAndSleepTurnedOffWhileLocked_StillLooksForNothing()
    {
        StartWatcher();
        _session.Raise(SessionSwitchReason.SessionLock);
        var looks = _programs.FindCount;

        _autoPause.Enabled = false;
        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(looks, _programs.FindCount);
        Assert.False(_time.HasPendingTimer);
    }

    // The pause from the tray menu is no reason to stop looking.
    [Fact]
    public void WhilePausedFromTheTray_KeepsLooking()
    {
        StartWatcher();
        var looks = _programs.FindCount;

        _lighting.SetPaused(PauseReasons.Manual, true);
        _time.Advance(Interval);

        Assert.Equal(looks + 1, _programs.FindCount);
    }

    [Fact]
    public void BeforeStart_ReadsNothing()
    {
        _watcher = new ProgramWatcher(_profiles, _lighting, _programs, _autoPause, _time);

        Assert.Equal(0, _store.LoadCount);
        Assert.False(_time.HasPendingTimer);
    }

    [Fact]
    public void ProgramStarts_LoadsItsProfileWithinOneInterval()
    {
        StartWatcher();

        _programs.Running.Add("EldenRing.exe");
        _time.Advance(Interval - TimeSpan.FromMilliseconds(1));
        Assert.Equal(Green, ShownColor());

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(Red, ShownColor());
    }

    [Fact]
    public void ProgramStops_GoesBackToTheLightingFromBefore()
    {
        StartWatcher();
        StartProgram(EldenRing);
        var changes = 0;
        _lighting.LightingChanged += (_, _) => changes++;

        StopProgram(EldenRing);

        Assert.Equal(Green, ShownColor());
        Assert.Equal(1, changes);
    }

    // The game's scene is on while it runs; afterwards the scene from before is on again.
    [Fact]
    public void ProgramStartsAndStops_TheActiveSceneFollows()
    {
        StartWatcher();
        _profiles.Load("Nacht");

        StartProgram(EldenRing);
        Assert.Equal("Gamen", _profiles.ActiveName);

        StopProgram(EldenRing);
        Assert.Equal("Nacht", _profiles.ActiveName);
    }

    [Fact]
    public void ProgramStops_NoSceneBefore_LeavesNoneActive()
    {
        StartWatcher();
        StartProgram(EldenRing);

        StopProgram(EldenRing);

        Assert.Null(_profiles.ActiveName);
    }

    [Fact]
    public void SecondProgramStops_TheSceneOfTheFirstIsActiveAgain()
    {
        StartWatcher();
        StartProgram(EldenRing);
        StartProgram(Forza);
        Assert.Equal("Race", _profiles.ActiveName);

        StopProgram(Forza);
        Assert.Equal("Gamen", _profiles.ActiveName);
    }

    [Fact]
    public void ChangedInTheAppWhileRunning_KeepsThatAfterTheStop()
    {
        StartWatcher();
        StartProgram(EldenRing);
        ShowColor(Orange);

        StopProgram(EldenRing);

        Assert.Equal(Orange, ShownColor());
    }

    [Fact]
    public void ProfileLoadedWhileRunning_KeepsItAfterTheStop()
    {
        StartWatcher();
        StartProgram(EldenRing);
        _profiles.Load("Nacht");

        StopProgram(EldenRing);

        Assert.Equal(Orange, ShownColor());
    }

    [Fact]
    public void AlreadyRunningAtStart_LoadsItsProfileRightAwayAndGoesBackToTheSavedLighting()
    {
        _programs.Running.Add(EldenRing);

        StartWatcher();
        Assert.Equal(Red, ShownColor());

        StopProgram(EldenRing);
        Assert.Equal(Green, ShownColor());
    }

    // So the engine saves the lighting from before the program, not the program's profile.
    [Fact]
    public void Dispose_WhileAProgramRuns_PutsTheLightingFromBeforeBack()
    {
        StartWatcher();
        StartProgram(EldenRing);

        _watcher!.Dispose();

        Assert.Equal(Green, ShownColor());
    }

    [Fact]
    public void Dispose_AfterAChangeWhileAProgramRuns_KeepsThatChange()
    {
        StartWatcher();
        StartProgram(EldenRing);
        ShowColor(Orange);

        _watcher!.Dispose();

        Assert.Equal(Orange, ShownColor());
    }

    [Fact]
    public void Dispose_WhileTwoProgramsRun_PutsTheLightingFromBeforeBothBack()
    {
        StartWatcher();
        StartProgram(EldenRing);
        StartProgram(Forza);

        _watcher!.Dispose();

        Assert.Equal(Green, ShownColor());
    }

    [Fact]
    public void Dispose_AfterAChangeBeforeTheSecondProgram_GoesBackToThatChange()
    {
        StartWatcher();
        StartProgram(EldenRing);
        ShowColor(Orange);
        StartProgram(Forza);

        _watcher!.Dispose();

        Assert.Equal(Orange, ShownColor());
    }

    // The next start loads the program's profile again, on top of the lighting saved at the stop.
    [Fact]
    public void EngineRestartsWhileAProgramRuns_EndsWithTheLightingFromBeforeTheProgram()
    {
        StartWatcher();
        StartProgram(EldenRing);
        _watcher!.Dispose();

        StartWatcher();
        Assert.Equal(Red, ShownColor());
        StopProgram(EldenRing);

        Assert.Equal(Green, ShownColor());
    }

    [Fact]
    public void ProgramSetWhileItRuns_LoadsOnlyAtItsNextStart()
    {
        StartWatcher();
        _programs.Running.Add("game.exe");

        _profiles.SetAutomation("Nacht", new ProfileAutomation(Program: "game.exe"));
        _time.Advance(Interval);

        Assert.Equal(Green, ShownColor());
    }

    [Fact]
    public void SecondProgramStopsFirst_ShowsTheFirstProgramAgain()
    {
        StartWatcher();
        StartProgram(EldenRing);
        StartProgram(Forza);
        Assert.Equal(Blue, ShownColor());

        StopProgram(Forza);
        Assert.Equal(Red, ShownColor());

        StopProgram(EldenRing);
        Assert.Equal(Green, ShownColor());
    }

    [Fact]
    public void FirstProgramStopsFirst_KeepsTheSecondUntilItStops()
    {
        StartWatcher();
        StartProgram(EldenRing);
        StartProgram(Forza);

        StopProgram(EldenRing);
        Assert.Equal(Blue, ShownColor());

        StopProgram(Forza);
        Assert.Equal(Green, ShownColor());
    }

    // The user changed the lighting while the first program ran: that stays once both stopped.
    [Fact]
    public void ChangedBeforeTheSecondProgram_GoesBackToThatChange()
    {
        StartWatcher();
        StartProgram(EldenRing);
        ShowColor(Orange);
        StartProgram(Forza);

        StopProgram(Forza);
        Assert.Equal(Orange, ShownColor());

        StopProgram(EldenRing);
        Assert.Equal(Orange, ShownColor());
    }

    [Fact]
    public void ChangedBeforeTheSecondProgram_FirstStopsFirst_StillGoesBackToThatChange()
    {
        StartWatcher();
        StartProgram(EldenRing);
        ShowColor(Orange);
        StartProgram(Forza);

        StopProgram(EldenRing);
        StopProgram(Forza);

        Assert.Equal(Orange, ShownColor());
    }

    [Fact]
    public void SameProgramOnTwoProfiles_LoadsTheFirstInAlphabeticalOrder()
    {
        _store.Profiles = [Profile("Race", Blue, EldenRing), Profile("Gamen", Red, EldenRing)];
        StartWatcher();

        StartProgram(EldenRing);

        Assert.Equal(Red, ShownColor());
    }

    [Fact]
    public void ProgramRemovedFromItsProfile_StopsWatchingWithoutGoingBack()
    {
        _store.Profiles = [Profile("Gamen", Red, EldenRing), Profile("Nacht", Orange)];
        StartWatcher();
        StartProgram(EldenRing);

        _profiles.SetAutomation("Gamen", ProfileAutomation.None);
        StopProgram(EldenRing);

        Assert.Equal(Red, ShownColor());
        Assert.False(_time.HasPendingTimer);
    }

    [Fact]
    public void ProgramSetFromTheApp_StartsWatching()
    {
        _store.Profiles = [Profile("Nacht", Orange)];
        StartWatcher();

        _profiles.SetAutomation("Nacht", new ProfileAutomation(Program: EldenRing));
        StartProgram(EldenRing);

        Assert.Equal(Orange, ShownColor());
    }

    [Fact]
    public void ProcessListCannotBeRead_ChangesNothingAndKeepsLooking()
    {
        StartWatcher();
        StartProgram(EldenRing);
        _programs.FindException = new InvalidOperationException("The process list could not be read.");

        _time.Advance(Interval);
        Assert.Equal(Red, ShownColor());

        _programs.FindException = null;
        StopProgram(EldenRing);
        Assert.Equal(Green, ShownColor());
    }

    [Fact]
    public void Dispose_StopsTheTimerAndListening()
    {
        StartWatcher();

        _watcher!.Dispose();
        _programs.Running.Add(EldenRing);
        _session.Raise(SessionSwitchReason.SessionLock);
        _session.Raise(SessionSwitchReason.SessionUnlock);
        _time.Advance(Interval);

        Assert.Equal(Green, ShownColor());
        Assert.False(_time.HasPendingTimer);
    }

    private static Profile Profile(string name, RgbColor color, string? program = null) =>
        new(name, Uniform(StaticLighting(color))) { Automation = new ProfileAutomation(Program: program) };

    // As the engine wires it, with the AutoPause of the test unless another is given.
    private void StartWatcher(AutoPause? autoPause = null)
    {
        _watcher = new ProgramWatcher(_profiles, _lighting, _programs, autoPause ?? _autoPause, _time);
        _watcher.Start();
    }

    private void GoAway(string away)
    {
        switch (away)
        {
            case "lock":
                _session.Raise(SessionSwitchReason.SessionLock);
                break;
            case "sleep":
                _power.RaiseSuspending();
                break;
            case "disconnect":
                _session.Raise(SessionSwitchReason.ConsoleDisconnect);
                break;
        }
    }

    private void ComeBack(string away)
    {
        switch (away)
        {
            case "lock":
                _session.Raise(SessionSwitchReason.SessionUnlock);
                break;
            case "sleep":
                _power.RaiseResumed();
                break;
            case "disconnect":
                _session.Raise(SessionSwitchReason.ConsoleConnect);
                break;
        }
    }

    private void StartProgram(string program)
    {
        _programs.Running.Add(program);
        _time.Advance(Interval);
    }

    private void StopProgram(string program)
    {
        _programs.Running.Remove(program);
        _time.Advance(Interval);
    }

    private RgbColor ShownColor() => _lighting.GetLighting(null).Color;

    private void ShowColor(RgbColor color) => _lighting.SetLighting(null, lighting => lighting with { Color = color });
}
