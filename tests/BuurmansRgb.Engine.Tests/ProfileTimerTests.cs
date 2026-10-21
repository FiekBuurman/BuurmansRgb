using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Engine.Power;
using BuurmansRgb.Engine.Profiles;
using static BuurmansRgb.Engine.Tests.LightingStates;

namespace BuurmansRgb.Engine.Tests;

public sealed class ProfileTimerTests : IDisposable
{
    // 1 June 2026 14:00 in Amsterdam (summer time, UTC+2): 23:00 is 9 hours away.
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan UntilElevenPm = TimeSpan.FromHours(9);
    private static readonly RgbColor StartColor = DeviceLighting.Default.Color;

    private readonly InMemoryProfileStore _store = new();
    private readonly LightingController _lighting = new([new RecordingDeviceProvider()]);
    private readonly FakeClockEvents _clock = new();
    private readonly FakePowerEvents _power = new();
    private readonly ProfileLibrary _profiles;
    private ClockTimeProvider _time = new(Start, DailyTimeTests.Amsterdam);
    private ProfileTimer? _timer;
    private ResumeRestorer? _restorer;

    public ProfileTimerTests() => _profiles = new ProfileLibrary(_store, _lighting);

    public void Dispose()
    {
        _timer?.Dispose();
        _restorer?.Dispose();
        _lighting.Dispose();
    }

    [Fact]
    public void NoProfileWithATime_NoTimerRuns()
    {
        _store.Profiles = [Profile("Gamen", Green)];

        StartTimer();

        Assert.False(_time.HasPendingTimer);
    }

    [Fact]
    public void BeforeStart_ReadsNothing()
    {
        _timer = new ProfileTimer(_profiles, _clock, _time);

        Assert.Equal(0, _store.LoadCount);
        Assert.False(_time.HasPendingTimer);
    }

    [Fact]
    public void LoadsTheProfileAtItsTime()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();

        _time.Advance(UntilElevenPm - TimeSpan.FromSeconds(1));
        Assert.Equal(StartColor, ShownColor());

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(Red, ShownColor());
    }

    [Fact]
    public void LoadsTheProfileAgainTheNextDay()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();
        _time.Advance(UntilElevenPm);
        ShowColor(Green);

        _time.Advance(TimeSpan.FromDays(1));

        Assert.Equal(Red, ShownColor());
    }

    [Fact]
    public void LoadedProfile_IsSavedAsTheCurrentLighting()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();
        var changes = 0;
        _lighting.LightingChanged += (_, _) => changes++;

        _time.Advance(UntilElevenPm);

        Assert.Equal(1, changes);
    }

    [Fact]
    public void TwoProfiles_EachLoadsAtItsOwnTime()
    {
        _store.Profiles = [Profile("Avond", Orange, new TimeOnly(20, 0)), Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();

        _time.Advance(TimeSpan.FromHours(6));
        Assert.Equal(Orange, ShownColor());

        _time.Advance(TimeSpan.FromHours(3));
        Assert.Equal(Red, ShownColor());
    }

    [Fact]
    public void TwoProfilesAtTheSameTime_LoadsTheFirstInAlphabeticalOrder()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0)), Profile("Avond", Orange, new TimeOnly(23, 0))];
        StartTimer();

        _time.Advance(UntilElevenPm);

        Assert.Equal(Orange, ShownColor());
    }

    [Fact]
    public void SettingATime_StartsTheTimerAndRemovingIt_StopsIt()
    {
        _store.Profiles = [Profile("Nacht", Red)];
        StartTimer();

        _profiles.SetAutomation("Nacht", new ProfileAutomation(new TimeOnly(23, 0)));
        Assert.True(_time.HasPendingTimer);

        _profiles.SetAutomation("Nacht", ProfileAutomation.None);
        Assert.False(_time.HasPendingTimer);
        _time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(StartColor, ShownColor());
    }

    [Fact]
    public void DeletingTheProfile_StopsTheTimer()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();

        _profiles.Delete("Nacht");

        Assert.False(_time.HasPendingTimer);
    }

    [Fact]
    public void AnotherTimeFromTheApp_LoadsAtTheNewTime()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();

        _profiles.SetAutomation("Nacht", new ProfileAutomation(new TimeOnly(15, 0)));
        _time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(Red, ShownColor());
    }

    // The pc sleeps from 14:00 until 00:00: the timers stand still, the clock does not.
    [Fact]
    public void SleptThroughTheTime_SkipsItAndLoadsTheNextDay()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();

        _time.MoveClock(TimeSpan.FromHours(10));
        _clock.RaiseChanged();
        _time.Advance(TimeSpan.FromHours(23) - TimeSpan.FromSeconds(1));
        Assert.Equal(StartColor, ShownColor());

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(Red, ShownColor());
    }

    // The timer goes off hours late by the clock, for example after a sleep that sent no event.
    [Fact]
    public void TimerHoursLate_SkipsTheTime()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();
        _time.MoveClock(TimeSpan.FromHours(3));

        _time.Advance(UntilElevenPm);

        Assert.Equal(StartColor, ShownColor());
        Assert.True(_time.HasPendingTimer);
    }

    [Fact]
    public void TimerLateWithinTheLimit_StillLoads()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();
        _time.MoveClock(ProfileTimer.LateLimit);

        _time.Advance(UntilElevenPm);

        Assert.Equal(Red, ShownColor());
    }

    [Fact]
    public void ClockSetForward_LoadsWhenTheClockShowsTheTime()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();

        _time.MoveClock(TimeSpan.FromHours(6));
        _clock.RaiseChanged();
        _time.Advance(TimeSpan.FromHours(3));

        Assert.Equal(Red, ShownColor());
    }

    // Without the clock event the timer goes off two hours early by the clock, and waits on.
    [Fact]
    public void ClockSetBack_WaitsUntilTheClockShowsTheTime()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();
        _time.MoveClock(TimeSpan.FromHours(-2));

        _time.Advance(UntilElevenPm);
        Assert.Equal(StartColor, ShownColor());

        _time.Advance(TimeSpan.FromHours(2));
        Assert.Equal(Red, ShownColor());
    }

    // On 25 October 2026 the clocks go back: from 23:00 on the 24th to 23:00 on the 25th is 25 hours.
    [Fact]
    public void AcrossTheSwitchToWinterTime_LoadsAtTheSameClockTime()
    {
        _time = new ClockTimeProvider(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero), DailyTimeTests.Amsterdam);
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();
        _time.Advance(TimeSpan.FromHours(9));
        ShowColor(Green);

        _time.Advance(TimeSpan.FromHours(24));
        Assert.Equal(Green, ShownColor());

        _time.Advance(TimeSpan.FromHours(1));
        Assert.Equal(Red, ShownColor());
    }

    [Fact]
    public void HeldWhileAProgramRuns_DoesNotShowTheProfile()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        var held = new List<RgbColor>();
        _timer = new ProfileTimer(_profiles, _clock, _time, name =>
        {
            held.Add(_profiles.GetLighting(name).Global.Color);
            return true;
        });
        _timer.Start();

        _time.Advance(UntilElevenPm);

        Assert.Equal([Red], held);
        Assert.Equal(StartColor, ShownColor());
    }

    [Fact]
    public void ProfilesCannotBeRead_StartsWithoutATimerAndWithoutThrowing()
    {
        _store.LoadException = new IOException("The file is in use.");

        StartTimer();

        Assert.False(_time.HasPendingTimer);
    }

    [Fact]
    public void Dispose_StopsTheTimerAndListening()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();

        _timer!.Dispose();
        _time.Advance(TimeSpan.FromDays(1));
        _profiles.Save("Werk");

        Assert.Equal(StartColor, ShownColor());
        Assert.False(_time.HasPendingTimer);
        Assert.False(_clock.HasListeners);
    }

    [Fact]
    public void CatchUp_IsOffAtFirst()
    {
        StartTimer();

        Assert.False(_timer!.CatchUp);
    }

    [Fact]
    public void CatchUpOff_AtTheStart_LoadsNothing()
    {
        _store.Profiles = [Profile("Ochtend", Blue, new TimeOnly(7, 0))];
        var changes = CountChanges();

        StartTimer();

        Assert.Equal(StartColor, ShownColor());
        Assert.Equal(0, changes());
    }

    // As before there was CatchUp: the time is skipped, also once the lights are back.
    [Fact]
    public void CatchUpOff_AfterWaking_SkipsTheTimeAndLoadsTheNextDay()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();

        SleepFor(TimeSpan.FromHours(10));
        WaitForTheLights();
        Assert.Equal(StartColor, ShownColor());

        _time.Advance(TimeSpan.FromHours(23) - ResumeRestorer.DefaultDelays[0]);
        Assert.Equal(Red, ShownColor());
    }

    // At 14:00 on Monday, 07:00 that morning passed last.
    [Fact]
    public void CatchUp_AtTheStart_LoadsTheSceneWhoseTimePassedLast()
    {
        _store.Profiles = [Profile("Avond", Orange, new TimeOnly(20, 0)), Profile("Nacht", Red, new TimeOnly(23, 0)), Profile("Ochtend", Blue, new TimeOnly(7, 0))];

        StartTimer(catchUp: true);

        Assert.Equal(Blue, ShownColor());
        Assert.Equal("Ochtend", _profiles.ActiveName);
    }

    // Saturday 07:00 and Sunday 20:00: on Monday, Sunday evening passed last.
    [Fact]
    public void CatchUp_AtTheStart_CountsOnlyTheChosenDays()
    {
        _store.Profiles = [Profile("Avond", Orange, new TimeOnly(20, 0), WeekDays.Sunday), Profile("Ochtend", Blue, new TimeOnly(7, 0), WeekDays.Saturday)];

        StartTimer(catchUp: true);

        Assert.Equal(Orange, ShownColor());
    }

    [Fact]
    public void CatchUp_AtTheStart_TwoScenesAtTheSameTime_LoadsTheFirstInAlphabeticalOrder()
    {
        _store.Profiles = [Profile("Werk", Red, new TimeOnly(7, 0)), Profile("Ochtend", Blue, new TimeOnly(7, 0))];

        StartTimer(catchUp: true);

        Assert.Equal(Blue, ShownColor());
    }

    // The scene from before the restart is the scene of the time already.
    [Fact]
    public void CatchUp_AtTheStart_TheSceneIsOnAlready_LoadsNothing()
    {
        _store.Profiles = [Profile("Ochtend", Blue, new TimeOnly(7, 0))];
        _profiles.RestoreActive("Ochtend");
        var changes = CountChanges();

        StartTimer(catchUp: true);

        Assert.Equal(0, changes());
    }

    // What happened while the engine did not run is not known: the time wins.
    [Fact]
    public void CatchUp_AtTheStart_AnotherSceneIsOn_GivesWayToTheTime()
    {
        _store.Profiles = [Profile("Gamen", Green), Profile("Ochtend", Blue, new TimeOnly(7, 0))];
        _profiles.Load("Gamen");

        StartTimer(catchUp: true);

        Assert.Equal(Blue, ShownColor());
        Assert.Equal("Ochtend", _profiles.ActiveName);
    }

    [Fact]
    public void CatchUp_AtTheStart_NoSceneWithATime_LoadsNothing()
    {
        _store.Profiles = [Profile("Gamen", Green)];
        var changes = CountChanges();

        StartTimer(catchUp: true);

        Assert.Equal(0, changes());
    }

    [Fact]
    public void CatchUp_AtTheStart_WhileAProgramRuns_HoldsTheScene()
    {
        _store.Profiles = [Profile("Ochtend", Blue, new TimeOnly(7, 0))];
        var held = new List<string>();

        StartTimer(catchUp: true, hold: name =>
        {
            held.Add(name);
            return true;
        });

        Assert.Equal(["Ochtend"], held);
        Assert.Equal(StartColor, ShownColor());
    }

    // Asleep from 14:00 until 00:00: 23:00 passed in the sleep.
    [Fact]
    public void CatchUp_AfterWaking_LoadsTheSceneOnceTheLightsAreBack()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimerCatchingUpAfterWaking();

        SleepFor(TimeSpan.FromHours(10));
        _time.Advance(ResumeRestorer.DefaultDelays[0] - TimeSpan.FromMilliseconds(1));
        Assert.Equal(StartColor, ShownColor());

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(Red, ShownColor());
        Assert.Equal("Nacht", _profiles.ActiveName);
    }

    // On from the settings app, without a restart.
    [Fact]
    public void CatchUpTurnedOnAfterTheStart_CountsFromTheNextWakeUp()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();

        _timer!.CatchUp = true;
        Assert.Equal(StartColor, ShownColor());

        SleepFor(TimeSpan.FromHours(10));
        WaitForTheLights();
        Assert.Equal(Red, ShownColor());
    }

    [Fact]
    public void CatchUp_AfterSleepingThroughTwoTimes_LoadsTheLastOne()
    {
        _store.Profiles = [Profile("Avond", Orange, new TimeOnly(20, 0)), Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimerCatchingUpAfterWaking();

        SleepFor(TimeSpan.FromHours(12));
        WaitForTheLights();

        Assert.Equal(Red, ShownColor());
    }

    // 20:00 came while awake and the user chose a color after it; the sleep from 20:00 to 22:00
    // passed no time, so the color stays.
    [Fact]
    public void CatchUp_AfterWaking_NoTimePassedInTheSleep_KeepsTheLighting()
    {
        _store.Profiles = [Profile("Avond", Orange, new TimeOnly(20, 0)), Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimerCatchingUpAfterWaking();
        _time.Advance(TimeSpan.FromHours(6));
        ShowColor(Green);

        SleepFor(TimeSpan.FromHours(2));
        WaitForTheLights();

        Assert.Equal(Green, ShownColor());
    }

    // Asleep from Monday 14:00 until Tuesday 08:00: Nacht only comes in the weekend, so Monday
    // 20:00 passed last.
    [Fact]
    public void CatchUp_AfterWaking_CountsOnlyTheChosenDays()
    {
        _store.Profiles = [Profile("Avond", Orange, new TimeOnly(20, 0)), Profile("Nacht", Red, new TimeOnly(23, 0), WeekDays.Weekend)];
        StartTimerCatchingUpAfterWaking();

        SleepFor(TimeSpan.FromHours(18));
        WaitForTheLights();

        Assert.Equal(Orange, ShownColor());
    }

    [Fact]
    public void CatchUp_AfterWakingWhileAProgramRuns_HoldsTheScene()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        var held = new List<string>();
        StartTimerCatchingUpAfterWaking(hold: name =>
        {
            held.Add(name);
            return true;
        });

        SleepFor(TimeSpan.FromHours(10));
        WaitForTheLights();

        Assert.Equal(["Nacht"], held);
        Assert.Equal(StartColor, ShownColor());
    }

    // Back to sleep before the lights were back: the next wake-up still knows the time passed.
    [Fact]
    public void CatchUp_AsleepAgainBeforeTheLightsAreBack_LoadsAfterTheNextWakeUp()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimerCatchingUpAfterWaking();

        SleepFor(TimeSpan.FromHours(10));
        SleepFor(TimeSpan.FromHours(1));
        Assert.Equal(StartColor, ShownColor());

        WaitForTheLights();
        Assert.Equal(Red, ShownColor());
    }

    // A wake-up without the clock event, such as when it comes later.
    [Fact]
    public void CatchUp_AfterWakingWithoutAClockEvent_StillLoads()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimerCatchingUpAfterWaking();

        _power.RaiseSuspending();
        _time.MoveClock(TimeSpan.FromHours(10));
        _power.RaiseResumed();
        WaitForTheLights();

        Assert.Equal(Red, ShownColor());
    }

    // Only waking up and the start catch up; a new time on the clock is skipped as before.
    [Fact]
    public void CatchUp_ClockSetPastATime_LoadsNothing()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimerCatchingUpAfterWaking();

        _time.MoveClock(TimeSpan.FromHours(10));
        _clock.RaiseChanged();
        _time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(StartColor, ShownColor());
    }

    // The clock jumps from 14:00 past 23:00 to 00:00, the user chooses a color and the pc sleeps
    // for an hour: no time passed in the sleep, so the color stays.
    [Fact]
    public void CatchUp_ClockSetPastATimeThenASleepWithoutATime_KeepsTheLighting()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimerCatchingUpAfterWaking();
        _time.MoveClock(TimeSpan.FromHours(10));
        _clock.RaiseChanged();
        ShowColor(Green);

        SleepFor(TimeSpan.FromHours(1));
        WaitForTheLights();

        Assert.Equal(Green, ShownColor());
    }

    // The clock event of a wake-up comes after the lights are back; the next sleep passes no time.
    [Fact]
    public void CatchUp_ClockEventAfterTheLightsAreBack_TheNextSleepWithoutATime_KeepsTheLighting()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimerCatchingUpAfterWaking();
        _power.RaiseSuspending();
        _time.MoveClock(TimeSpan.FromHours(10));
        _power.RaiseResumed();
        WaitForTheLights();
        _clock.RaiseChanged();
        Assert.Equal(Red, ShownColor());
        ShowColor(Green);

        SleepFor(TimeSpan.FromHours(1));
        WaitForTheLights();

        Assert.Equal(Green, ShownColor());
    }

    // Awake at 23:00:30, within LateLimit: the clock event moves the timer to tomorrow, so only
    // catching up loads it.
    [Fact]
    public void CatchUp_WakingHalfAMinuteAfterTheTime_LoadsTheScene()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimerCatchingUpAfterWaking();

        SleepFor(UntilElevenPm + TimeSpan.FromSeconds(30));
        WaitForTheLights();

        Assert.Equal(Red, ShownColor());
    }

    // As before there was CatchUp.
    [Fact]
    public void CatchUpOff_WakingHalfAMinuteAfterTheTime_SkipsItAndLoadsTheNextDay()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimer();

        SleepFor(UntilElevenPm + TimeSpan.FromSeconds(30));
        WaitForTheLights();
        Assert.Equal(StartColor, ShownColor());

        _time.Advance(TimeSpan.FromDays(1) - TimeSpan.FromSeconds(30) - ResumeRestorer.DefaultDelays[0]);
        Assert.Equal(Red, ShownColor());
    }

    // Without Suspending it is not known when the pc went to sleep.
    [Fact]
    public void CatchUp_WakingWithoutSuspending_LoadsNothing()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimerCatchingUpAfterWaking();

        _time.MoveClock(TimeSpan.FromHours(10));
        _clock.RaiseChanged();
        _power.RaiseResumed();
        WaitForTheLights();

        Assert.Equal(StartColor, ShownColor());
    }

    [Fact]
    public void CatchUpChanged_IsRaisedOnlyWhenItChanges()
    {
        StartTimer();
        var raised = 0;
        _timer!.CatchUpChanged += (_, _) => raised++;

        _timer.CatchUp = true;
        _timer.CatchUp = true;
        _timer.CatchUp = false;

        Assert.Equal(2, raised);
    }

    [Fact]
    public void Dispose_StopsCatchingUpAfterWaking()
    {
        _store.Profiles = [Profile("Nacht", Red, new TimeOnly(23, 0))];
        StartTimerCatchingUpAfterWaking();

        _timer!.Dispose();
        SleepFor(TimeSpan.FromHours(10));
        WaitForTheLights();

        Assert.Equal(StartColor, ShownColor());
    }

    private static Profile Profile(string name, RgbColor color, TimeOnly? loadAt = null, WeekDays days = WeekDays.Every) =>
        new(name, Uniform(StaticLighting(color))) { Automation = new ProfileAutomation(loadAt, Days: days) };

    // With the restorer of the engine, so a wake-up restores the lights before the timer catches up.
    private void StartTimer(bool catchUp = false, Func<string, bool>? hold = null)
    {
        _restorer = new ResumeRestorer(_power, _lighting.RestoreDevices, _time, ResumeRestorer.DefaultDelays);
        _timer = new ProfileTimer(_profiles, _clock, _time, hold, _restorer) { CatchUp = catchUp };
        _timer.Start();
    }

    // On after the start, so only waking up catches up.
    private void StartTimerCatchingUpAfterWaking(Func<string, bool>? hold = null)
    {
        StartTimer(hold: hold);
        _timer!.CatchUp = true;
    }

    // The pc sleeps: the clock moves on and the timers stand still. Waking up raises the clock
    // event, as SystemClockEvents does, and starts the restores.
    private void SleepFor(TimeSpan asleep)
    {
        _power.RaiseSuspending();
        _time.MoveClock(asleep);
        _clock.RaiseChanged();
        _power.RaiseResumed();
    }

    private void WaitForTheLights() => _time.Advance(ResumeRestorer.DefaultDelays[0]);

    private Func<int> CountChanges()
    {
        var changes = 0;
        _lighting.LightingChanged += (_, _) => changes++;
        return () => changes;
    }

    private RgbColor ShownColor() => _lighting.GetLighting(null).Color;

    private void ShowColor(RgbColor color) => _lighting.SetLighting(null, lighting => lighting with { Color = color });
}
