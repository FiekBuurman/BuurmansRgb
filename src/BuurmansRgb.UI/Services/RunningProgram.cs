namespace BuurmansRgb.UI.Services;

// A program that shows a window now, for "Kies uit wat nu draait": its exe name, which goes into the
// field, and the name a user knows it by, such as "Outlook" for olk.exe; the exe name itself for a
// program without another name. RunningProgramItem shows it.
public sealed record RunningProgram(string ExeName, string Name)
{
    public RunningProgram(string exeName)
        : this(exeName, exeName)
    {
    }
}
