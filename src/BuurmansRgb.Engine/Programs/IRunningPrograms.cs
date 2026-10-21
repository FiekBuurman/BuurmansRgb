namespace BuurmansRgb.Engine.Programs;

internal interface IRunningPrograms
{
    // The names among names (file names as ProgramNames gives them, any case) that have at least
    // one process running now, spelled as in names.
    IReadOnlySet<string> Find(IReadOnlySet<string> names);
}
