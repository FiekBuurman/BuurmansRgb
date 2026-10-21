using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;

namespace BuurmansRgb.UI.ViewModels;

// What a scene card shows of a saved profile: five bulbs with the colors of its lighting for all
// devices, and when it loads by itself, or else what it shows, such as "Regenboog · 80%". Of more
// times only the first fits, with how many more: "Werkdagen om 07:00 en nog 1".
internal static class SceneLook
{
    public const int DotCount = 5;

    // Until the engine has told what the scene looks like.
    public static IReadOnlyList<SceneDot> Unknown { get; } = [.. Enumerable.Repeat(new SceneDot(RgbColor.Black, false), DotCount)];

    public static IReadOnlyList<SceneDot> DotsOf(LightingInfo lighting)
    {
        if (!lighting.On)
        {
            return Unknown;
        }

        var loop = (lighting.Loop ?? ColorLoop.Default).Colors;
        return [.. Enumerable.Range(0, DotCount).Select(i => new SceneDot(lighting.EffectId switch
        {
            EffectIds.Rainbow => RgbColor.FromHsv(i / (double)DotCount, 1.0, 1.0),
            EffectIds.ColorLoop => loop[i % loop.Count],
            _ => lighting.Color,
        }, true))];
    }

    // A program that counts only in the foreground says so: "Bij vlc.exe op de voorgrond".
    public static string Describe(LightingInfo lighting, ProfileAutomation automation) =>
        Describe(lighting, automation.Times, automation is { Program: { } program, ForegroundOnly: true } ? UiText.Format(Strings.SceneProgramInForeground, program) : automation.Program);

    private static string Describe(LightingInfo lighting, IReadOnlyList<LoadTime> times, string? program) => (times, program) switch
    {
        ([var time], { }) => UiText.Format(Strings.SceneAtTimeAndProgram, UiText.Days(time.Days), time.At, program),
        ([var time], null) => UiText.Format(Strings.SceneAtTime, UiText.Days(time.Days), time.At),
        ([var time, ..], { }) => UiText.Format(Strings.SceneAtTimesAndProgram, UiText.Days(time.Days), time.At, times.Count - 1, program),
        ([var time, ..], null) => UiText.Format(Strings.SceneAtTimes, UiText.Days(time.Days), time.At, times.Count - 1),
        (_, { }) => UiText.Format(Strings.SceneWithProgram, program),
        _ when !lighting.On => Strings.SceneOff,
        _ => UiText.Format(Strings.SceneLighting, EffectOption.Create(lighting.EffectId).DisplayName, lighting.Brightness * 100.0),
    };
}
