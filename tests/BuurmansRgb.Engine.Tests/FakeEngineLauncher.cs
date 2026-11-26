using BuurmansRgb.Engine.Restart;

namespace BuurmansRgb.Engine.Tests;

// The launcher of the new engine, which remembers the arguments it got.
internal sealed class FakeEngineLauncher : IEngineLauncher
{
    public bool Result { get; set; } = true;

    public List<IReadOnlyList<string>> Starts { get; } = [];

    public bool Start(IReadOnlyList<string> arguments)
    {
        Starts.Add([.. arguments]);
        return Result;
    }
}
