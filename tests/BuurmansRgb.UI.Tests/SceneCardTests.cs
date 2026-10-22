using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class SceneCardTests
{
    private readonly SceneCard _card = new("Klusmodus");

    [Fact]
    public void New_HasDarkBulbsUntilTheEngineTellsWhatItLooksLike()
    {
        Assert.Equal(5, _card.Dots.Count);
        Assert.All(_card.Dots, dot => Assert.False(dot.IsLit));
        Assert.Equal("Klusmodus aanzetten", _card.LoadAccessibleName);
        Assert.Equal("Klusmodus aanpassen", _card.EditAccessibleName);
    }

    [Fact]
    public void Show_StaticColor_FiveBulbsInThatColorAndTheEffectWithItsBrightness()
    {
        _card.Show(new LightingInfo(Orange, 0.8, EffectIds.Static), ProfileAutomation.None);

        Assert.All(_card.Dots, dot => Assert.Equal(new SceneDot(Orange, true), dot));
        Assert.Equal("Statisch · 80%", _card.Description);
    }

    [Fact]
    public void Show_Rainbow_GoesRoundTheColors()
    {
        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.Rainbow), ProfileAutomation.None);

        Assert.Equal(Red, _card.Dots[0].Color);
        Assert.Equal(5, _card.Dots.Select(dot => dot.Color).Distinct().Count());
        Assert.Equal("Regenboog · 100%", _card.Description);
    }

    [Fact]
    public void Show_ColorLoop_RepeatsTheColorsOfTheLoop()
    {
        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.ColorLoop, Loop: new ColorLoop([Red, Blue])), ProfileAutomation.None);

        Assert.Equal([Red, Blue, Red, Blue, Red], _card.Dots.Select(dot => dot.Color));
    }

    [Fact]
    public void Show_Off_DarkBulbsAndSaysSo()
    {
        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.Static, On: false), ProfileAutomation.None);

        Assert.All(_card.Dots, dot => Assert.False(dot.IsLit));
        Assert.Equal("Alles uit", _card.Description);
    }

    [Theory]
    [InlineData("23:00", null, "Elke dag om 23:00")]
    [InlineData(null, "eldenring.exe", "Bij eldenring.exe")]
    [InlineData("07:30", "eldenring.exe", "Elke dag om 07:30 · bij eldenring.exe")]
    [InlineData("09:00", null, "Werkdagen om 09:00", WeekDays.Workdays)]
    [InlineData("10:00", null, "In het weekend om 10:00", WeekDays.Weekend)]
    [InlineData("19:00", "eldenring.exe", "ma, wo, vr om 19:00 · bij eldenring.exe", WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday)]
    public void Show_LoadsByItself_SaysWhen(string? loadAt, string? program, string expected, WeekDays days = WeekDays.Every)
    {
        var automation = loadAt is null
            ? new ProfileAutomation(Program: program)
            : new ProfileAutomation(TimeOnly.Parse(loadAt, System.Globalization.CultureInfo.InvariantCulture), program, days);

        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.Static, On: false), automation);

        Assert.Equal(expected, _card.Description);
    }

    // Only the first time fits on the card, with how many more there are.
    [Theory]
    [InlineData(2, null, "Werkdagen om 07:00 en nog 1")]
    [InlineData(3, null, "Werkdagen om 07:00 en nog 2")]
    [InlineData(2, "eldenring.exe", "Werkdagen om 07:00 en nog 1 · bij eldenring.exe")]
    public void Show_MoreTimes_SaysTheFirstAndHowManyMore(int count, string? program, string expected)
    {
        LoadTime[] times = [new(new TimeOnly(7, 0), WeekDays.Workdays), new(new TimeOnly(9, 0), WeekDays.Weekend), new(new TimeOnly(23, 0))];

        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.Static), new ProfileAutomation(times[..count], program));

        Assert.Equal(expected, _card.Description);
    }
}
