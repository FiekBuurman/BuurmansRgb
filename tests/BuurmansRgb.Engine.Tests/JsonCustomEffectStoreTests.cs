using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Settings;

namespace BuurmansRgb.Engine.Tests;

public sealed class JsonCustomEffectStoreTests : IDisposable
{
    private readonly TempDirectory _directory = new();
    private readonly JsonCustomEffectStore _store;

    public JsonCustomEffectStoreTests() => _store = new JsonCustomEffectStore(_directory.File("effects.json"));

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void DefaultFilePath_IsNextToTheSettings()
    {
        Assert.Equal(Path.Combine(Path.GetDirectoryName(JsonSettingsStore.DefaultFilePath)!, "effects.json"), JsonCustomEffectStore.DefaultFilePath);
    }

    [Fact]
    public void Load_NoFile_ReturnsNoEffects() => Assert.Empty(_store.Load());

    [Fact]
    public void Save_ThenLoad_RoundTripsTheEffects()
    {
        CustomEffect[] effects =
        [
            new("custom:1", "Disco", [new CustomEffectStep(Red, 0.5, 0), new CustomEffectStep([Green, Blue, Orange], 2, 1.5)]),
            new("custom:2", "Rustig", [new CustomEffectStep(Blue)]),
        ];

        _store.Save(effects);

        Assert.Equal(effects, _store.Load());
    }

    [Fact]
    public void Save_WritesReadableJson()
    {
        _store.Save([new CustomEffect("custom:1", "Disco", [new CustomEffectStep([Red, Blue], 0.5, 1)])]);

        var json = File.ReadAllText(_directory.File("effects.json"));

        Assert.Contains("\"id\": \"custom:1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"Disco\"", json, StringComparison.Ordinal);
        Assert.Contains("\"#FF0000\"", json, StringComparison.Ordinal);
        Assert.Contains("\"hold\": 0.5", json, StringComparison.Ordinal);
        Assert.Contains("\"fade\": 1", json, StringComparison.Ordinal);
    }

    // A file edited by hand: what does not read is left out, the rest still counts.
    [Fact]
    public void Load_ValuesThatDoNotRead_AreLeftOut()
    {
        File.WriteAllText(_directory.File("effects.json"), """
            { "effects": [
                { "id": "custom:1", "name": " Disco ", "steps": [
                    { "colors": ["#FF0000", "paars", "#0000FF"], "hold": "lang", "fade": 120 },
                    { "colors": [] },
                    "geen stap",
                    { "colors": ["#00FF00"], "hold": -3 } ] },
                { "id": "rainbow", "name": "Geen eigen id", "steps": [ { "colors": ["#FF0000"] } ] },
                { "id": "custom:3", "name": "", "steps": [ { "colors": ["#FF0000"] } ] },
                { "id": "custom:4", "name": "Zonder stappen" },
                { "id": "custom:1", "name": "Zelfde id", "steps": [ { "colors": ["#FF0000"] } ] },
                { "id": "custom:5", "name": "disco", "steps": [ { "colors": ["#FF0000"] } ] },
                42
            ] }
            """);

        var effect = Assert.Single(_store.Load());

        Assert.Equal(
            new CustomEffect("custom:1", "Disco", [new CustomEffectStep([Red, Blue], CustomEffects.DefaultHoldSeconds, CustomEffects.MaxSeconds), new CustomEffectStep(Green, 0, CustomEffects.DefaultFadeSeconds)]),
            effect);
    }

    [Fact]
    public void Load_MoreThanTheMost_KeepsTheFirst()
    {
        _store.Save([.. Enumerable.Range(0, CustomEffects.MaxEffects + 2).Select(i => new CustomEffect($"custom:{i}", $"Effect {i}", [new CustomEffectStep(Red)]))]);

        Assert.Equal(CustomEffects.MaxEffects, _store.Load().Count);
    }

    [Fact]
    public void Load_StepsAndColorsOverTheMost_KeepTheFirst()
    {
        var step = new CustomEffectStepSettings { Colors = [.. Enumerable.Repeat<RgbColor?>(Red, CustomEffects.MaxLeds + 1)] };
        var settings = new CustomEffectSettings { Id = JsonText.From("custom:1"), Name = JsonText.From("Lang"), Steps = [.. Enumerable.Repeat(step, CustomEffects.MaxSteps + 1)] };

        var effect = settings.ToCustomEffect()!;

        Assert.Equal(CustomEffects.MaxSteps, effect.Steps.Count);
        Assert.Equal(CustomEffects.MaxLeds, effect.Steps[0].Colors.Count);
    }

    // Not valid JSON: put aside, so the next save does not overwrite what the user wrote.
    [Fact]
    public void Load_InvalidJson_MovesTheFileAsideAndReturnsNone()
    {
        File.WriteAllText(_directory.File("effects.json"), "{ \"effects\": [ ");

        Assert.Empty(_store.Load());
        Assert.False(File.Exists(_directory.File("effects.json")));
        Assert.True(File.Exists(_store.BrokenFilePath));
    }
}
