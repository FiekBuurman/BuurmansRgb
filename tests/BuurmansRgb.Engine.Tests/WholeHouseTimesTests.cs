using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Profiles;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

// "Uit om" and "Aan om" of Hele huis on Apparaten: the times of the scene "Alles uit", or of an
// internal profile, on the timer of the scene times, with a fake clock.
public sealed class WholeHouseTimesTests : IDisposable
{
    // 1 June 2026 14:00 in Amsterdam (summer time, UTC+2): 23:00 is 9 hours away, 07:00 17.
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan UntilElevenPm = TimeSpan.FromHours(9);
    private static readonly TimeSpan UntilSevenAm = TimeSpan.FromHours(17);
    private static readonly LoadTime ElevenPm = new(new TimeOnly(23, 0));
    private static readonly LoadTime SevenAm = new(new TimeOnly(7, 0));

    private readonly InMemoryProfileStore _store = new();
    private readonly LightingController _lighting = new([new RecordingDeviceProvider()]);
    private readonly FakeClockEvents _clock = new();
    private readonly FakePowerEvents _power = new();
    private readonly ProfileLibrary _profiles;
    private ClockTimeProvider _time = new(Start, DailyTimeTests.Amsterdam);
    private ProfileTimer? _timer;
    private ResumeRestorer? _restorer;

    public WholeHouseTimesTests() => _profiles = new ProfileLibrary(_store, _lighting);

    public void Dispose()
    {
        _timer?.Dispose();
        _restorer?.Dispose();
        _lighting.Dispose();
    }

    [Fact]
    public void Off_WithoutTimes_IsNone_AndNoTimerRuns()
    {
        _store.Profiles = [AllOffScene("Alles uit")];

        StartTimer();

        Assert.Equal(WholeHouseTimes.None with { OffScene = "Alles uit" }, _profiles.GetWholeHouseTimes());
        Assert.False(_time.HasPendingTimer);
    }

    // One source of truth: the times are those of the scene, as Scènes shows them.
    [Fact]
    public void UitOm_GoesOnTheSceneAllesUit_AndATimeThereShowsHere()
    {
        _store.Profiles = [AllOffScene("Alles uit"), new Profile("Gamen", Uniform(StaticLighting(Green)))];

        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm]));

        Assert.Equal([ElevenPm], _profiles.GetAutomation("Alles uit").Times);
        Assert.DoesNotContain(_store.Profiles, profile => profile.IsInternal);

        _profiles.SetAutomation("Alles uit", new ProfileAutomation([SevenAm, ElevenPm]));

        Assert.Equal(new WholeHouseTimes([SevenAm, ElevenPm], OffScene: "Alles uit"), _profiles.GetWholeHouseTimes());
    }

    // A program of the scene stays; only its times are "Uit om".
    [Fact]
    public void UitOm_OnTheScene_KeepsItsProgram()
    {
        _store.Profiles = [AllOffScene("Alles uit") with { Automation = new ProfileAutomation(Program: "slaap.exe") }];

        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm]));

        Assert.Equal(new ProfileAutomation([ElevenPm], "slaap.exe"), _profiles.GetAutomation("Alles uit"));
    }

    // The role counts, not the name: renamed by hand, it is still "Alles uit"; a scene of that name
    // without the role is not.
    [Fact]
    public void UitOm_FindsTheSceneByItsRole_NotByItsName()
    {
        _store.Profiles = [AllOffScene("Bedtijd"), new Profile("Alles uit", Uniform(StaticLighting(Green)))];

        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm]));

        Assert.Equal("Bedtijd", _profiles.GetWholeHouseTimes().OffScene);
        Assert.Equal([ElevenPm], _profiles.GetAutomation("Bedtijd").Times);
        Assert.Empty(_profiles.GetAutomation("Alles uit").Times);
    }

    [Fact]
    public void UitOm_PutsOnTheSceneAllesUit_AtItsTime()
    {
        _store.Profiles = [AllOffScene("Alles uit")];
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm]));
        StartTimer();

        _time.Advance(UntilElevenPm - TimeSpan.FromSeconds(1));
        Assert.True(_lighting.State.IsAnyOn);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.False(_lighting.State.IsAnyOn);
        Assert.Equal(StartScenes.AllOff.Global, _lighting.State.Global);
        Assert.Equal("Alles uit", _profiles.ActiveName);
    }

    // Without the scene: an internal profile, which is no scene, and which switches every device off
    // with its own lighting, as "Alles uit" in the tray menu.
    [Fact]
    public void UitOm_WithoutTheScene_SwitchesEverythingOff_AndKeepsTheLighting()
    {
        _store.Profiles = [new Profile("Gamen", Uniform(StaticLighting(Green)))];
        _lighting.SetLighting(null, lighting => lighting with { Color = Red });
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm]));
        StartTimer();

        _time.Advance(UntilElevenPm);

        Assert.Equal(new WholeHouseTimes([ElevenPm]), _profiles.GetWholeHouseTimes());
        Assert.Equal(["Gamen"], _profiles.Names);
        Assert.False(_lighting.State.IsAnyOn);
        Assert.Equal(Red, _lighting.State.Global.Color);
    }

    [Fact]
    public void UitOm_Internal_EverythingOffAlready_ChangesNothing()
    {
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm]));
        _lighting.SetLighting(null, lighting => lighting with { On = false });
        StartTimer();
        var changes = CountChanges();

        _time.Advance(UntilElevenPm);

        Assert.Equal(0, changes());
    }

    // Deleting the scene keeps "Uit om", on an internal profile from then on.
    [Fact]
    public void DeletingAllesUit_KeepsUitOm()
    {
        _store.Profiles = [AllOffScene("Alles uit")];
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm], [SevenAm]));

        _profiles.Delete("Alles uit");

        Assert.Empty(_profiles.Names);
        Assert.Equal(new WholeHouseTimes([ElevenPm], [SevenAm]), _profiles.GetWholeHouseTimes());
    }

    // Truus: the lighting of the evening comes back in the morning, not the calm white of Alles uit.
    [Fact]
    public void AanOm_AfterAllesUit_PutsBackTheLightingFromBefore()
    {
        _store.Profiles = [AllOffScene("Alles uit")];
        _lighting.SetLighting(null, lighting => lighting with { Color = Red, EffectId = EffectIds.Rainbow });
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm]));
        _lighting.SetLighting(null, lighting => lighting with { Color = Orange });
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm], [SevenAm]));
        _lighting.SetLighting(null, lighting => lighting with { Color = Blue });
        StartTimer();

        _time.Advance(UntilElevenPm);
        Assert.False(_lighting.State.IsAnyOn);

        _time.Advance(UntilSevenAm - UntilElevenPm);
        Assert.True(_lighting.State.Global.On);
        Assert.Equal(Blue, _lighting.State.Global.Color);
        Assert.Equal(EffectIds.Rainbow, _lighting.State.Global.EffectId);
        Assert.Null(_profiles.ActiveName);
    }

    // What burned when everything went off by hand comes back on too.
    [Fact]
    public void AanOm_AfterAllesUitOverLightsThatWereOff_PutsThemBackOn()
    {
        _store.Profiles = [AllOffScene("Alles uit")];
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm], [SevenAm]));
        _lighting.SetLighting(null, lighting => lighting with { Color = Green, On = false });
        StartTimer();

        _time.Advance(UntilElevenPm);
        _time.Advance(UntilSevenAm - UntilElevenPm);

        Assert.True(_lighting.State.Global.On);
        Assert.Equal(Green, _lighting.State.Global.Color);
    }

    // Loading Alles uit twice does not keep its own off lighting as the one from before.
    [Fact]
    public void AllesUitTwice_KeepsTheLightingFromBeforeTheFirst()
    {
        _store.Profiles = [AllOffScene("Alles uit")];
        _profiles.SetWholeHouseTimes(new WholeHouseTimes(On: [SevenAm]));
        _lighting.SetLighting(null, lighting => lighting with { Color = Red });

        _profiles.Load("Alles uit");
        _profiles.Load("Alles uit");

        Assert.Equal(Red, Assert.Single(_store.Profiles, profile => profile.Role == ProfileRole.BackOn).Lighting.Global.Color);
    }

    // Henk switched everything off in the tray menu: in the morning it all goes on with its own lighting.
    [Fact]
    public void AanOm_AfterAnotherWayOff_SwitchesEverythingOn()
    {
        _store.Profiles = [AllOffScene("Alles uit")];
        _lighting.SetLighting(null, lighting => lighting with { Color = Red });
        _profiles.SetWholeHouseTimes(new WholeHouseTimes(On: [SevenAm]));
        _lighting.SetLighting(null, lighting => lighting with { Color = Green, On = false });
        StartTimer();

        _time.Advance(UntilSevenAm);

        Assert.True(_lighting.State.Global.On);
        Assert.Equal(Green, _lighting.State.Global.Color);
    }

    // Something is on already, chosen since: that stays.
    [Fact]
    public void AanOm_ADeviceIsOn_ChangesNothing()
    {
        _profiles.SetWholeHouseTimes(new WholeHouseTimes(On: [SevenAm]));
        StartTimer();
        var changes = CountChanges();

        _time.Advance(UntilSevenAm);

        Assert.Equal(0, changes());
    }

    [Fact]
    public void AanOm_IsInternal_AndNoTimesTakeItAway()
    {
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm], [SevenAm]));
        Assert.Equal(2, _store.Profiles.Count(profile => profile.IsInternal));
        Assert.Empty(_profiles.Names);

        _profiles.SetWholeHouseTimes(WholeHouseTimes.None);

        Assert.Empty(_store.Profiles);
        Assert.Equal(WholeHouseTimes.None, _profiles.GetWholeHouseTimes());
    }

    [Fact]
    public void SetWholeHouseTimes_RaisesChanged_AndTooManyTimesThrow()
    {
        var changed = 0;
        _profiles.Changed += (_, _) => changed++;

        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm]));
        Assert.Throws<ArgumentException>(() => _profiles.SetWholeHouseTimes(
            new WholeHouseTimes([.. Enumerable.Range(0, ProfileAutomation.MaxTimes + 1).Select(hour => new LoadTime(new TimeOnly(hour, 0)))])));

        Assert.Equal(1, changed);
        Assert.Equal(new WholeHouseTimes([ElevenPm]), _profiles.GetWholeHouseTimes());
    }

    // Started at 23:30 with "Gemiste tijden inhalen": Uit om at 23:00 passed last.
    [Fact]
    public void CatchUp_AtTheStart_UitOmPassedLast_SwitchesEverythingOff()
    {
        _time = new ClockTimeProvider(Start + UntilElevenPm + TimeSpan.FromMinutes(30), DailyTimeTests.Amsterdam);
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm], [SevenAm]));

        StartTimer(catchUp: true);

        Assert.False(_lighting.State.IsAnyOn);
    }

    // Started at 08:00 after a night with the pc off, the devices off as Uit om left them.
    [Fact]
    public void CatchUp_AtTheStart_AanOmPassedLast_SwitchesEverythingOn()
    {
        _time = new ClockTimeProvider(Start + UntilSevenAm + TimeSpan.FromHours(1), DailyTimeTests.Amsterdam);
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm], [SevenAm]));
        _lighting.SetLighting(null, lighting => lighting with { Color = Red, On = false });

        StartTimer(catchUp: true);

        Assert.True(_lighting.State.Global.On);
        Assert.Equal(Red, _lighting.State.Global.Color);
    }

    [Fact]
    public void WithoutCatchUp_AtTheStart_NothingIsCaughtUp()
    {
        _time = new ClockTimeProvider(Start + UntilElevenPm + TimeSpan.FromMinutes(30), DailyTimeTests.Amsterdam);
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm]));

        StartTimer();

        Assert.True(_lighting.State.IsAnyOn);
    }

    // Asleep from 14:00 until 06:00 over Uit om at 23:00: off once the lights are back.
    [Fact]
    public void CatchUp_AfterWaking_UitOmPassedWhileAsleep_SwitchesEverythingOff()
    {
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm], [SevenAm]));
        StartTimer();
        _timer!.CatchUp = true;

        SleepFor(TimeSpan.FromHours(16));
        _time.Advance(ResumeRestorer.DefaultDelays[0]);

        Assert.False(_lighting.State.IsAnyOn);
    }

    // As a scene at its time: while a program shows its scene, Uit om waits for the program to stop.
    [Fact]
    public void UitOm_Internal_WhileAProgramRuns_IsHeldWithEverythingOff()
    {
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm]));
        var held = new List<HeldLighting>();
        _timer = new ProfileTimer(_profiles, _clock, _time, next =>
        {
            held.Add(next(new HeldLighting(Uniform(StaticLighting(Red)), "Gamen")));
            return true;
        });
        _timer.Start();

        _time.Advance(UntilElevenPm);

        var (lightingHeld, sceneHeld) = Assert.Single(held);
        Assert.False(lightingHeld.IsAnyOn);
        Assert.Equal(Red, lightingHeld.Global.Color);
        Assert.Null(sceneHeld);
        Assert.True(_lighting.State.IsAnyOn);
    }

    // Uit om 23:00 waits for the game; Aan om at 07:00 comes during the game; the game stops at
    // 07:30: the lighting of the evening comes back on, not every device off.
    [Fact]
    public void AanOm_WhileAProgramHoldsUitOm_PutsTheDevicesOnOnceItStops()
    {
        _store.Profiles = [AllOffScene("Alles uit"), new Profile("Gamen", Uniform(StaticLighting(Green)))];
        _profiles.SetWholeHouseTimes(new WholeHouseTimes([ElevenPm], [SevenAm]));
        _lighting.SetLighting(null, lighting => lighting with { Color = Red });
        var game = new FakeProgramHold();
        _timer = new ProfileTimer(_profiles, _clock, _time, game.Hold);
        _timer.Start();
        game.Start(_lighting.State, _profiles.ActiveName);
        _profiles.Load("Gamen");

        _time.Advance(UntilElevenPm);
        Assert.Equal(Green, _lighting.State.Global.Color);
        Assert.Equal("Alles uit", game.Held.Scene);
        Assert.False(game.Held.Lighting.IsAnyOn);

        _time.Advance(UntilSevenAm - UntilElevenPm);
        Assert.Equal(Green, _lighting.State.Global.Color);

        _time.Advance(TimeSpan.FromMinutes(30));
        _profiles.Show(game.Held.Lighting, game.Held.Scene);

        Assert.True(_lighting.State.Global.On);
        Assert.Equal(Red, _lighting.State.Global.Color);
        Assert.Null(_profiles.ActiveName);
    }

    // Started during a game and stopped with something on: Aan om keeps that.
    [Fact]
    public void AanOm_WhileAProgramRuns_OverLightingThatIsOn_KeepsIt()
    {
        _profiles.SetWholeHouseTimes(new WholeHouseTimes(On: [SevenAm]));
        var game = new FakeProgramHold();
        _timer = new ProfileTimer(_profiles, _clock, _time, game.Hold);
        _timer.Start();
        var evening = Uniform(StaticLighting(Red));
        game.Start(evening, "Avond");

        _time.Advance(UntilSevenAm);

        Assert.Same(evening, game.Held.Lighting);
        Assert.Equal("Avond", game.Held.Scene);
    }

    private static Profile AllOffScene(string name) => new(name, StartScenes.AllOff) { Role = ProfileRole.AllOff };

    // A program that shows its scene from Start on, as ProgramWatcher.HoldUntilProgramsStop: a time
    // changes what comes back once it stops (Held).
    private sealed class FakeProgramHold
    {
        private bool _running;

        public HeldLighting Held { get; private set; }

        public void Start(LightingState before, string? scene)
        {
            _running = true;
            Held = new HeldLighting(before, scene);
        }

        public bool Hold(Func<HeldLighting, HeldLighting> next)
        {
            if (_running)
            {
                Held = next(Held);
            }

            return _running;
        }
    }

    // With the restorer of the engine, so a wake-up restores the lights before the timer catches up.
    private void StartTimer(bool catchUp = false)
    {
        _restorer = new ResumeRestorer(_power, _lighting.RestoreDevices, _time, ResumeRestorer.DefaultDelays);
        _timer = new ProfileTimer(_profiles, _clock, _time, holdWhileProgramsRun: null, _restorer) { CatchUp = catchUp };
        _timer.Start();
        _timer.CatchUpAtStart();
    }

    private void SleepFor(TimeSpan asleep)
    {
        _power.RaiseSuspending();
        _time.MoveClock(asleep);
        _clock.RaiseChanged();
        _power.RaiseResumed();
    }

    private Func<int> CountChanges()
    {
        var changes = 0;
        _lighting.LightingChanged += (_, _) => changes++;
        return () => changes;
    }
}
