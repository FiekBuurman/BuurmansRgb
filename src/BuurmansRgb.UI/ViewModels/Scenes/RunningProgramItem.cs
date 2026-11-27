using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.ViewModels;

// A program in "Kies uit wat nu draait". The list shows it, and a screen reader reads it, as
// "Outlook (olk.exe)": the name a user knows it by with its exe name behind it; a program without
// another name is just its exe name. Picked, the field gets the exe name.
public sealed record RunningProgramItem(RunningProgram Program)
{
    public string Label => Program.Name == Program.ExeName ? Program.ExeName : UiText.Format(Strings.ProgramPickItem, Program.Name, Program.ExeName);

    // WPF shows an item, and names it for UI Automation, by its ToString.
    public override string ToString() => Label;
}
