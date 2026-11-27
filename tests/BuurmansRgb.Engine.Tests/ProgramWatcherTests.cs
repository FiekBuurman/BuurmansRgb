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
    private const string Valorant = "valorant.exe";
    private const string Cs2 = "cs2.exe";
    private const string Apex = "r5apex.exe";

    private static readonly TimeSpan Interval = ProgramWatcher.DefaultPollInterval;

    private readonly InMemoryProfileStore _store = new();
    private readonly LightingController _lighting = new([new RecordingDeviceProvider()]);
    private readonly FakeRunningPrograms _programs = new();
    private readonly FakeForegroundWindow _foreground = new();
    private readonly FakePowerEvents _power = new();
    private readonly FakeSessionEvents _session = new();
    private readonly ClockTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
    private readonly ProfileLibrary _profiles;
    private readonly AutoPause _autoPause;
    private ProgramWatcher? _watcher;

    public ProgramWatcherTests()
    {
        _profiles = new ProfileLibrary(_store, _lighting);
        _autoPause = AutoPauses.WithFakes(_lighting.SetPaused, power: _power, session: _session);
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
    [InlineData(Absence.Locked, true)]
    [InlineData(Absence.Locked, false)]
    [InlineData(Absence.Asleep, true)]
    [InlineData(Absence.Asleep, false)]
    [InlineData(Absence.AsleepUntilUnlocked, true)]
    [InlineData(Absence.AsleepUntilUnlocked, false)]
    [InlineData(Absence.ConsoleDisconnected, true)]
    [InlineData(Absence.ConsoleDisconnected, false)]
    [InlineData(Absence.RemoteDisconnected, true)]
    [InlineData(Absence.RemoteDisconnected, false)]
    public void WhileAway_LooksForNothing_AndRightAfterLooksAtOnce(Absence away, bool pauseOnLockAndSleep)
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

    // A time that comes on the lock screen after the game stopped is not held until unlocking.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HoldWhileLocked_GameStoppedMeanwhile_LooksOnceAndHoldsNothing(bool pauseOnLockAndSleep)
    {
        _autoPause.Enabled = pauseOnLockAndSleep;
        StartWatcher();
        StartProgram(EldenRing);
        _session.Raise(SessionSwitchReason.SessionLock);
        _programs.Running.Remove(EldenRing);
        var looks = _programs.FindCount;

        Assert.False(_watcher!.HoldScene(_profiles, "Nacht"));

        Assert.Equal(looks + 1, _programs.FindCount);
        Assert.Equal(Green, ShownColor());
        Assert.False(_time.HasPendingTimer);
    }

    [Fact]
    public void HoldWhileLocked_GameStillRuns_LooksOnceAndHoldsUntilItStops()
    {
        StartWatcher();
        StartProgram(EldenRing);
        _session.Raise(SessionSwitchReason.SessionLock);
        var looks = _programs.FindCount;

        Assert.True(_watcher!.HoldScene(_profiles, "Nacht"));
        Assert.Equal(looks + 1, _programs.FindCount);
        Assert.Equal(Red, ShownColor());

        _session.Raise(SessionSwitchReason.SessionUnlock);
        StopProgram(EldenRing);
        Assert.Equal(Orange, ShownColor());
    }

    // Only on the lock screen, and only with a game whose scene it could hold.
    [Fact]
    public void Hold_WhileSomebodyIsThereOrWithoutAGame_LooksNoExtraTime()
    {
        StartWatcher();
        _session.Raise(SessionSwitchReason.SessionLock);
        var looks = _programs.FindCount;
        Assert.False(_watcher!.HoldScene(_profiles, "Nacht"));
        Assert.Equal(looks, _programs.FindCount);

        _session.Raise(SessionSwitchReason.SessionUnlock);
        StartProgram(EldenRing);
        looks = _programs.FindCount;
        Assert.True(_watcher!.HoldScene(_profiles, "Nacht"));
        Assert.Equal(looks, _programs.FindCount);
    }

    // next gets the lighting from before the game with the scene that was on then, and what it
    // gives is what comes back once the game stops.
    [Fact]
    public void Hold_NextGetsTheLightingFromBefore_AndWhatItGivesComesBack()
    {
        _profiles.Load("Nacht");
        StartWatcher();
        StartProgram(EldenRing);
        HeldLighting? given = null;

        Assert.True(_watcher!.HoldUntilProgramsStop(held =>
        {
            given = held;
            return new HeldLighting(_profiles.Get("Race").Lighting, "Race");
        }));

        Assert.Equal(Orange, given?.Lighting.Global.Color);
        Assert.Equal("Nacht", given?.Scene);
        Assert.Equal(Red, ShownColor());

        StopProgram(EldenRing);
        Assert.Equal(Blue, ShownColor());
        Assert.Equal("Race", _profiles.ActiveName);
    }

    // An engine that starts late after sign-in may start on the lock screen.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StartedWhileLocked_LooksOnlyAfterUnlocking(bool pauseOnLockAndSleep)
    {
        _session.IsLocked = true;
        using var autoPause = AutoPauses.WithFakes(_lighting.SetPaused, power: _power, session: _session);
        autoPause.Enabled = pauseOnLockAndSleep;
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
        _watcher = new ProgramWatcher(_profiles, _lighting, _programs, _autoPause, _foreground, _time);

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

        _profiles.SetAutomation("Nacht", new SceneAutomation(Program: "game.exe"));
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

    // One scene for Valorant, CS2 and Apex: each of them loads it.
    [Theory]
    [InlineData(Valorant)]
    [InlineData(Cs2)]
    [InlineData("R5Apex.EXE")]
    public void ProfileWithMorePrograms_EachOfThemLoadsIt(string program)
    {
        _store.Profiles = [Profile("Gamen", Red, Valorant, Cs2, Apex)];
        StartWatcher();

        StartProgram(program);
        Assert.Equal(Red, ShownColor());

        StopProgram(program);
        Assert.Equal(Green, ShownColor());
    }

    // Two programs of the same scene stack as two of different scenes: the lighting from before
    // comes back once the last of them stops, whichever stops first.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TwoProgramsOfOneProfile_GoBackOnlyWhenBothStopped(bool firstStopsFirst)
    {
        _store.Profiles = [Profile("Gamen", Red, Valorant, Cs2)];
        StartWatcher();
        StartProgram(Valorant);
        StartProgram(Cs2);

        StopProgram(firstStopsFirst ? Valorant : Cs2);
        Assert.Equal(Red, ShownColor());
        Assert.Equal("Gamen", _profiles.ActiveName);

        StopProgram(firstStopsFirst ? Cs2 : Valorant);
        Assert.Equal(Green, ShownColor());
    }

    // Alt+Tab from one program of a foreground scene to another of the same scene keeps the scene,
    // without the lighting from before in between; a program of no scene then brings it back.
    [Fact]
    public void ForegroundSceneWithTwoPrograms_SwitchingBetweenThemKeepsIt()
    {
        _store.Profiles = [Profile("Gamen", Red, foregroundOnly: true, Valorant, Cs2), Profile("Nacht", Orange)];
        StartWatcher();
        _profiles.Load("Nacht");
        BringToFront(Valorant);
        Assert.Equal(Red, ShownColor());
        var shown = new List<RgbColor>();
        _lighting.LightingChanged += (_, _) => shown.Add(ShownColor());

        BringToFront(Cs2);
        Assert.Equal(Red, ShownColor());
        Assert.Equal("Gamen", _profiles.ActiveName);
        Assert.DoesNotContain(Orange, shown);

        _foreground.Bring("explorer.exe");
        Assert.Equal(Orange, ShownColor());
        Assert.Equal("Nacht", _profiles.ActiveName);
    }

    // A time that comes while two programs of one scene run waits until both stopped, whichever
    // stops first.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HoldWhileTwoProgramsOfOneProfileRun_WaitsUntilBothStopped(bool firstStopsFirst)
    {
        _store.Profiles = [Profile("Gamen", Red, Valorant, Cs2), Profile("Nacht", Orange)];
        StartWatcher();
        StartProgram(Valorant);
        StartProgram(Cs2);

        Assert.True(_watcher!.HoldScene(_profiles, "Nacht"));
        Assert.Equal(Red, ShownColor());

        StopProgram(firstStopsFirst ? Valorant : Cs2);
        Assert.Equal(Red, ShownColor());
        Assert.Equal("Gamen", _profiles.ActiveName);

        StopProgram(firstStopsFirst ? Cs2 : Valorant);
        Assert.Equal(Orange, ShownColor());
        Assert.Equal("Nacht", _profiles.ActiveName);
    }

    // A scene of some devices with two programs: once both stopped, every device is as before,
    // the ones of the scene and the others, whichever stops first.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SceneOfSomeDevicesWithTwoPrograms_AllDevicesAsBeforeOnceBothStopped(bool firstStopsFirst)
    {
        _store.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Red))) { DeviceIds = ["strip"], Automation = new SceneAutomation(Times: null, Programs: [Valorant, Cs2]) }];
        var before = WithDevices(StaticLighting(Green), new Dictionary<string, DeviceLighting> { ["fan"] = StaticLighting(Orange) });
        _profiles.Show(before, scene: null);
        StartWatcher();

        StartProgram(Valorant);
        StartProgram(Cs2);
        Assert.Equal(StaticLighting(Red), _lighting.State.For("strip"));
        Assert.Equal(StaticLighting(Orange), _lighting.State.For("fan"));
        Assert.Equal(StaticLighting(Green), _lighting.State.Global);

        StopProgram(firstStopsFirst ? Valorant : Cs2);
        Assert.Equal(StaticLighting(Red), _lighting.State.For("strip"));
        Assert.Equal(StaticLighting(Orange), _lighting.State.For("fan"));

        StopProgram(firstStopsFirst ? Cs2 : Valorant);
        Assert.Equal(StaticLighting(Green), _lighting.State.For("strip"));
        Assert.Equal(StaticLighting(Orange), _lighting.State.For("fan"));
        Assert.Equal(StaticLighting(Green), _lighting.State.Global);
        Assert.Null(_profiles.ActiveName);
    }

    // A name on two scenes goes with the first in alphabetical order, as one program does; the
    // other names of the second scene still load it.
    [Fact]
    public void NameOnTwoProfilesWithMorePrograms_GoesWithTheFirstAndTheOthersStillCount()
    {
        _store.Profiles = [Profile("Race", Blue, Cs2, Forza), Profile("Gamen", Red, Valorant, Cs2)];
        StartWatcher();

        StartProgram(Cs2);
        Assert.Equal(Red, ShownColor());
        StopProgram(Cs2);

        StartProgram(Forza);
        Assert.Equal(Blue, ShownColor());
    }

    // A name taken off the list stops counting; the others go on.
    [Fact]
    public void ProgramTakenOffTheList_StopsWatchingOnlyThatOne()
    {
        _store.Profiles = [Profile("Gamen", Red, Valorant, Cs2)];
        StartWatcher();

        _profiles.SetAutomation("Gamen", new SceneAutomation(Times: null, Programs: [Cs2]));
        StartProgram(Valorant);
        Assert.Equal(Green, ShownColor());

        StartProgram(Cs2);
        Assert.Equal(Red, ShownColor());
    }

    // More names cost no more looks: all of them go in one look.
    [Fact]
    public void ProfileWithMorePrograms_LooksForAllOfThemAtOnce()
    {
        _store.Profiles = [Profile("Gamen", Red, Valorant, Cs2, Apex)];
        StartWatcher();
        var looks = _programs.FindCount;

        _time.Advance(Interval);

        Assert.Equal(looks + 1, _programs.FindCount);
        Assert.Equal([Cs2, Apex, Valorant], _programs.LastFound.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ProgramRemovedFromItsProfile_StopsWatchingWithoutGoingBack()
    {
        _store.Profiles = [Profile("Gamen", Red, EldenRing), Profile("Nacht", Orange)];
        StartWatcher();
        StartProgram(EldenRing);

        _profiles.SetAutomation("Gamen", SceneAutomation.None);
        StopProgram(EldenRing);

        Assert.Equal(Red, ShownColor());
        Assert.False(_time.HasPendingTimer);
    }

    [Fact]
    public void ProgramSetFromTheApp_StartsWatching()
    {
        _store.Profiles = [Profile("Nacht", Orange)];
        StartWatcher();

        _profiles.SetAutomation("Nacht", new SceneAutomation(Program: EldenRing));
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
        _time.Advance(Interval);

        Assert.Equal(Green, ShownColor());
        Assert.False(_time.HasPendingTimer);
    }

    [Fact]
    public void Dispose_StopsListeningForAway()
    {
        var away = new FakeAwayState();
        StartWatcher(away);
        Assert.Equal(1, away.Listeners);

        _watcher!.Dispose();

        Assert.Equal(0, away.Listeners);
    }

    // How nobody is at the pc, and how that ends: AsleepUntilUnlocked wakes up without a Resume
    // from SystemEvents, so only unlocking ends the sleep.
    public enum Absence
    {
        Locked,
        Asleep,
        AsleepUntilUnlocked,
        ConsoleDisconnected,
        RemoteDisconnected,
    }

    private static Profile Profile(string name, RgbColor color, string? program = null) =>
        new(name, Uniform(StaticLighting(color))) { Automation = new SceneAutomation(Program: program) };

    private static Profile Profile(string name, RgbColor color, params string[] programs) =>
        new(name, Uniform(StaticLighting(color))) { Automation = new SceneAutomation(Times: null, Programs: programs) };

    private static Profile Profile(string name, RgbColor color, bool foregroundOnly, params string[] programs) =>
        new(name, Uniform(StaticLighting(color))) { Automation = new SceneAutomation(Times: null, Programs: programs, ForegroundOnly: foregroundOnly) };

    // As the engine wires it, with the AutoPause of the test unless another is given.
    private void StartWatcher(IAwayState? away = null)
    {
        _watcher = new ProgramWatcher(_profiles, _lighting, _programs, away ?? _autoPause, _foreground, _time);
        _watcher.Start();
    }

    private void GoAway(Absence away)
    {
        switch (away)
        {
            case Absence.Locked:
                _session.Raise(SessionSwitchReason.SessionLock);
                break;
            case Absence.Asleep or Absence.AsleepUntilUnlocked:
                _power.RaiseSuspending();
                break;
            case Absence.ConsoleDisconnected:
                _session.Raise(SessionSwitchReason.ConsoleDisconnect);
                break;
            case Absence.RemoteDisconnected:
                _session.Raise(SessionSwitchReason.RemoteDisconnect);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(away), away, null);
        }
    }

    private void ComeBack(Absence away)
    {
        switch (away)
        {
            case Absence.Locked or Absence.AsleepUntilUnlocked:
                _session.Raise(SessionSwitchReason.SessionUnlock);
                break;
            case Absence.Asleep:
                _power.RaiseResumed();
                break;
            case Absence.ConsoleDisconnected:
                _session.Raise(SessionSwitchReason.ConsoleConnect);
                break;
            case Absence.RemoteDisconnected:
                _session.Raise(SessionSwitchReason.RemoteConnect);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(away), away, null);
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

    // A window of program comes to the front; a program in front runs.
    private void BringToFront(string program)
    {
        _programs.Running.Add(program);
        _foreground.Bring(program);
    }

    private RgbColor ShownColor() => _lighting.GetLighting(null).Color;

    private void ShowColor(RgbColor color) => _lighting.SetLighting(null, lighting => lighting with { Color = color });
}
