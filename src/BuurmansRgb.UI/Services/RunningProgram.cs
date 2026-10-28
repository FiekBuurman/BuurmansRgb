using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Services;

// A program to pick from "Kies uit wat nu draait": its exe name, which goes into the field, and the
// name a user knows it by, such as "Outlook" for olk.exe. The list shows it, and a screen reader
// reads it, as "Outlook (olk.exe)"; a program without another name is just its exe name.
public sealed record RunningProgram(string ExeName, string Name)
{
    public RunningProgram(string exeName)
        : this(exeName, exeName)
    {
    }

    public string Label => Name == ExeName ? ExeName : UiText.Format(Strings.ProgramPickItem, Name, ExeName);

    // WPF shows an item, and names it for UI Automation, by its ToString.
    public override string ToString() => Label;
}
