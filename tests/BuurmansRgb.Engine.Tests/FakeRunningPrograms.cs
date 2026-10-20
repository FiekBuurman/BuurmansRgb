using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Tests;

internal sealed class FakeRunningPrograms : IRunningPrograms
{
    public HashSet<string> Running { get; } = new(ProgramNames.Comparer);

    public Exception? FindException { get; set; }

    public int FindCount { get; private set; }

    public IReadOnlySet<string> Find(IReadOnlySet<string> names)
    {
        FindCount++;
        if (FindException is not null)
        {
            throw FindException;
        }

        return new HashSet<string>(names.Where(Running.Contains), ProgramNames.Comparer);
    }
}
