using System.Text.Json;
using BuurmansRgb.Core;

namespace BuurmansRgb.Contracts.Tests;

public sealed class CustomEffectsTests
{
    [Theory]
    [InlineData("custom:1a2b3c4d", true)]
    [InlineData("custom:a-b", true)]
    [InlineData("custom:", false)]
    [InlineData("custom:ABC", false)]
    [InlineData("custom:a b", false)]
    [InlineData("custom:123456789012345678901234567890123", false)]
    [InlineData("rainbow", false)]
    [InlineData(null, false)]
    public void IsCustom_OnlyThePrefixWithLowercaseLettersDigitsOrDashes(string? effectId, bool expected)
    {
        Assert.Equal(expected, CustomEffects.IsCustom(effectId));
    }

    [Fact]
    public void NewId_IsCustomAndNewEachTime()
    {
        var first = CustomEffects.NewId();

        Assert.True(CustomEffects.IsCustom(first));
        Assert.NotEqual(first, CustomEffects.NewId());
    }

    [Theory]
    [InlineData("  Disco  ", "Disco")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("Twee\nregels", null)]
    [InlineData(null, null)]
    public void NormalizeName_TrimsOrRejects(string? name, string? expected)
    {
        Assert.Equal(expected, CustomEffects.NormalizeName(name));
    }

    [Fact]
    public void NormalizeName_TooLong_IsRejected()
    {
        Assert.Null(CustomEffects.NormalizeName(new string('a', CustomEffects.MaxNameLength + 1)));
        Assert.NotNull(CustomEffects.NormalizeName(new string('a', CustomEffects.MaxNameLength)));
    }

    [Fact]
    public void FirstFreeName_SkipsTheTakenNames()
    {
        HashSet<string> taken = ["Effect 1", "Effect 2", "Effect 4"];

        Assert.Equal("Effect 3", CustomEffects.FirstFreeName(number => $"Effect {number}", taken.Contains));
    }

    // With MaxEffects effects, all of them on one of the names, the next one is still free.
    [Fact]
    public void FirstFreeName_AllEffectsOnTheFirstNames_GivesTheNext()
    {
        var names = Enumerable.Range(1, CustomEffects.MaxEffects).Select(number => $"Effect {number}").ToHashSet();

        Assert.Equal($"Effect {CustomEffects.MaxEffects + 1}", CustomEffects.FirstFreeName(number => $"Effect {number}", names.Contains));
    }

    [Fact]
    public void Normalize_ValidEffect_TrimsTheName()
    {
        var effect = new CustomEffect("custom:1", " Disco ", [new CustomEffectStep(Red)]);

        Assert.Equal(effect with { Name = "Disco" }, CustomEffects.Normalize(effect));
    }

    public static TheoryData<CustomEffect> InvalidEffects() =>
    [
        new CustomEffect("rainbow", "Disco", [new CustomEffectStep(Red)]),
        new CustomEffect("custom:1", " ", [new CustomEffectStep(Red)]),
        new CustomEffect("custom:1", "Disco", []),
        new CustomEffect("custom:1", "Disco", [.. Enumerable.Repeat(new CustomEffectStep(Red), CustomEffects.MaxSteps + 1)]),
        new CustomEffect("custom:1", "Disco", [new CustomEffectStep(Red, holdSeconds: -1)]),
        new CustomEffect("custom:1", "Disco", [new CustomEffectStep(Red, fadeSeconds: CustomEffects.MaxSeconds + 1)]),
        new CustomEffect("custom:1", "Disco", [new CustomEffectStep(Red, holdSeconds: double.NaN)]),
        new CustomEffect("custom:1", "Disco", [new CustomEffectStep(Array.Empty<RgbColor>())]),
        new CustomEffect("custom:1", "Disco", [new CustomEffectStep([.. Enumerable.Repeat(Red, CustomEffects.MaxLeds + 1)])]),
    ];

    [Theory]
    [MemberData(nameof(InvalidEffects))]
    public void Normalize_InvalidEffect_Throws(CustomEffect effect)
    {
        Assert.Throws<ArgumentException>(() => CustomEffects.Normalize(effect));
    }

    [Fact]
    public void Normalize_TheMostStepsAndLeds_IsValid()
    {
        var step = new CustomEffectStep([.. Enumerable.Repeat(Red, CustomEffects.MaxLeds)], CustomEffects.MaxSeconds, CustomEffects.MaxSeconds);

        CustomEffects.Normalize(new CustomEffect("custom:1", "Disco", [.. Enumerable.Repeat(step, CustomEffects.MaxSteps)]));
    }

    [Fact]
    public void Step_IsPattern_OnlyWithMoreColors()
    {
        Assert.False(new CustomEffectStep(Red).IsPattern);
        Assert.True(new CustomEffectStep([Red, Blue]).IsPattern);
    }

    [Fact]
    public void Equals_ComparesTheStepsAndTheirColors()
    {
        var effect = new CustomEffect("custom:1", "Disco", [new CustomEffectStep([Red, Blue], 1, 2)]);

        Assert.Equal(effect, new CustomEffect("custom:1", "Disco", [new CustomEffectStep([Red, Blue], 1, 2)]));
        Assert.NotEqual(effect, new CustomEffect("custom:1", "Disco", [new CustomEffectStep([Blue, Red], 1, 2)]));
        Assert.NotEqual(effect, new CustomEffect("custom:1", "Disco", [new CustomEffectStep([Red, Blue], 1, 3)]));
    }

    // The steps are copied, so the caller cannot change the effect afterwards.
    [Fact]
    public void Constructor_CopiesTheLists()
    {
        var colors = new List<RgbColor> { Red };
        var steps = new List<CustomEffectStep> { new(colors) };
        var effect = new CustomEffect("custom:1", "Disco", steps);

        colors[0] = Blue;
        steps.Clear();

        Assert.Equal(Red, Assert.Single(effect.Steps).Colors[0]);
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var options = new JsonSerializerOptions { Converters = { new RgbColorJsonConverter() } };
        var effect = new CustomEffect("custom:1", "Disco", [new CustomEffectStep([Red, Blue], 0.5, 2), new CustomEffectStep(Blue)]);

        var read = JsonSerializer.Deserialize<CustomEffect>(JsonSerializer.Serialize(effect, options), options);

        Assert.Equal(effect, read);
    }
}
