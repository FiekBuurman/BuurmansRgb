using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
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
    private readonly ProfileLibrary _profiles;
    private ClockTimeProvider _time = new(Start, DailyTimeTests.Amsterdam);
    private ProfileTimer? _timer;

    public ProfileTimerTests() => _profiles = new ProfileLibrary(_store, _lighting);

    public void Dispose()
    {
        _timer?.Dispose();
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

    private static Profile Profile(string name, RgbColor color, TimeOnly? loadAt = null) =>
        new(name, Uniform(StaticLighting(color))) { Automation = new ProfileAutomation(loadAt) };

    private void StartTimer()
    {
        _timer = new ProfileTimer(_profiles, _clock, _time);
        _timer.Start();
    }

    private RgbColor ShownColor() => _lighting.GetLighting(null).Color;

    private void ShowColor(RgbColor color) => _lighting.SetLighting(null, lighting => lighting with { Color = color });
}
