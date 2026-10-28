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
        Assert.False(_card.IsShown);
        Assert.Equal("Klusmodus aanzetten", _card.LoadAccessibleName);
        Assert.Equal("Klusmodus aanpassen", _card.EditAccessibleName);
    }

    // WPF names the card after its ToString for a screen reader; the bulbs are one image that says
    // on or off, with each of their colors once, and have no name until the engine has told what
    // the scene looks like.
    [Fact]
    public void AccessibleNames_TheCardIsItsNameAndTheBulbsSayOnOrOffWithTheirColors()
    {
        Assert.Equal("Klusmodus", _card.ToString());
        Assert.Equal(string.Empty, _card.DotsAccessibleName);

        _card.Show(new LightingInfo(Orange, 0.8, EffectIds.Static), ProfileAutomation.None);
        Assert.Equal($"Aan: {Orange.ToHex()}", _card.DotsAccessibleName);

        _card.Show(new LightingInfo(Orange, 0.8, EffectIds.Fire, EffectColors: EffectColors.None.With(EffectIds.Fire, [Red, Green, Blue])), ProfileAutomation.None);
        Assert.Equal($"Aan: {Green.ToHex()}, {Blue.ToHex()}", _card.DotsAccessibleName);

        _card.Show(new LightingInfo(Orange, 0.8, EffectIds.Static, On: false), ProfileAutomation.None);
        Assert.Equal("Uit", _card.DotsAccessibleName);
    }

    [Fact]
    public void Show_StaticColor_FiveBulbsInThatColorAndTheEffectWithItsBrightness()
    {
        _card.Show(new LightingInfo(Orange, 0.8, EffectIds.Static), ProfileAutomation.None);

        Assert.All(_card.Dots, dot => Assert.Equal(new SceneDot(Orange, true), dot));
        Assert.Equal("Statisch · 80%", _card.Description);
    }

    // The card knows only the id of an own effect, not its name or steps.
    [Fact]
    public void Show_OwnEffect_IsAnOwnEffectInTheColorOfTheLamp()
    {
        _card.Show(new LightingInfo(Orange, 0.8, "custom:1a2b3c4d"), ProfileAutomation.None);

        Assert.All(_card.Dots, dot => Assert.Equal(new SceneDot(Orange, true), dot));
        Assert.Equal("Eigen effect · 80%", _card.Description);
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

    // Of the most colors, twelve, the five bulbs show the first five; the fire the five after its glow.
    [Fact]
    public void Show_TwelveColors_TheFirstFive()
    {
        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.ColorLoop, Loop: new ColorLoop(Twelve)), ProfileAutomation.None);
        Assert.Equal(Twelve.Take(5), _card.Dots.Select(dot => dot.Color));

        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.Fire, EffectColors: EffectColors.None.With(EffectIds.Fire, Twelve)), ProfileAutomation.None);
        Assert.Equal(Twelve.Skip(1).Take(5), _card.Dots.Select(dot => dot.Color));
    }

    // Fire has no color of its own: warm bulbs, whatever color the lamp has.
    [Fact]
    public void Show_Fire_GlowsWarmWithoutTheColorOfTheLamp()
    {
        _card.Show(new LightingInfo(Blue, 1.0, EffectIds.Fire), ProfileAutomation.None);

        Assert.All(_card.Dots, dot => Assert.True(dot.IsLit && dot.Color.R == 255 && dot.Color.B < dot.Color.G, $"{dot.Color.ToHex()} is not warm."));
        Assert.Equal(3, _card.Dots.Select(dot => dot.Color).Distinct().Count());
        Assert.Equal("Vuur · 100%", _card.Description);
    }

    [Theory]
    [InlineData(EffectIds.Twinkle, "Fonkelen")]
    [InlineData(EffectIds.Comet, "Komeet")]
    [InlineData(EffectIds.Scanner, "Scanner")]
    public void Show_AnEffectWithTheColorOfTheLamp_FiveBulbsInThatColor(string effectId, string name)
    {
        _card.Show(new LightingInfo(Orange, 0.5, effectId), ProfileAutomation.None);

        Assert.All(_card.Dots, dot => Assert.Equal(new SceneDot(Orange, true), dot));
        Assert.Equal($"{name} · 50%", _card.Description);
    }

    // The comet, the scanner and the twinkle show their colors in turn, not the lamp color.
    [Theory]
    [InlineData(EffectIds.Twinkle)]
    [InlineData(EffectIds.Comet)]
    [InlineData(EffectIds.Scanner)]
    public void Show_AnEffectWithAList_ShowsItsColorsInTurn(string effectId)
    {
        _card.Show(new LightingInfo(Orange, 1.0, effectId, EffectColors: EffectColors.None.With(effectId, [Red, Green])), ProfileAutomation.None);

        Assert.Equal([Red, Green, Red, Green, Red], _card.Dots.Select(dot => dot.Color));
        Assert.All(_card.Dots, dot => Assert.True(dot.IsLit));
    }

    // Blue fire: the bulbs glow up to the hottest color and back, without the dark glow of the first.
    [Fact]
    public void Show_FireWithAList_GlowsInItsColorsWithoutTheFirst()
    {
        var (glow, flame, hottest) = (new RgbColor(0, 0, 60), Blue, new RgbColor(0, 200, 255));

        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.Fire, EffectColors: EffectColors.None.With(EffectIds.Fire, [glow, flame, hottest])), ProfileAutomation.None);

        Assert.Equal([flame, hottest, flame, hottest, flame], _card.Dots.Select(dot => dot.Color));
    }

    // Two colors are both flames, and one color glows on every bulb.
    [Fact]
    public void Show_FireWithOneOrTwoColors_ShowsThemAll()
    {
        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.Fire, EffectColors: EffectColors.None.With(EffectIds.Fire, [Red, Green])), ProfileAutomation.None);
        Assert.Equal([Red, Green, Red, Green, Red], _card.Dots.Select(dot => dot.Color));

        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.Fire, EffectColors: EffectColors.None.With(EffectIds.Fire, [Green])), ProfileAutomation.None);
        Assert.All(_card.Dots, dot => Assert.Equal(Green, dot.Color));
    }

    // The warm fire looks as it did before there were lists: red, orange, yellow, orange, red.
    [Fact]
    public void Show_FireWithoutAList_GlowsAsBefore()
    {
        _card.Show(new LightingInfo(Blue, 1.0, EffectIds.Fire), ProfileAutomation.None);

        Assert.Equal(
            [new RgbColor(255, 24, 0), new RgbColor(255, 100, 0), new RgbColor(255, 190, 30), new RgbColor(255, 100, 0), new RgbColor(255, 24, 0)],
            _card.Dots.Select(dot => dot.Color));
    }

    // A list of the comet does not change the card of a scene with another effect.
    [Fact]
    public void Show_ListOfAnotherEffect_LeavesTheLampColor()
    {
        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.Static, EffectColors: EffectColors.None.With(EffectIds.Comet, [Red, Green])), ProfileAutomation.None);

        Assert.All(_card.Dots, dot => Assert.Equal(Orange, dot.Color));
    }

    // The dark bulbs of a new card are those of a scene with the lights off; that the engine has told
    // so is what gives them their name, and tells a screen reader.
    [Fact]
    public void Show_Off_DarkBulbsAndSaysSo()
    {
        var changed = new List<string?>();
        _card.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.Static, On: false), ProfileAutomation.None);

        Assert.All(_card.Dots, dot => Assert.False(dot.IsLit));
        Assert.Equal("Alles uit", _card.Description);
        Assert.True(_card.IsShown);
        Assert.Equal("Uit", _card.DotsAccessibleName);
        Assert.Contains(nameof(SceneCard.DotsAccessibleName), changed);
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

    [Fact]
    public void Show_ProgramInTheForeground_SaysSo()
    {
        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.Static), new ProfileAutomation(Program: "vlc.exe", ForegroundOnly: true));
        Assert.Equal("Bij vlc.exe op de voorgrond", _card.Description);

        _card.Show(new LightingInfo(Orange, 1.0, EffectIds.Static), new ProfileAutomation([new LoadTime(new TimeOnly(20, 0))], "vlc.exe", ForegroundOnly: true));
        Assert.Equal("Elke dag om 20:00 · bij vlc.exe op de voorgrond", _card.Description);
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
