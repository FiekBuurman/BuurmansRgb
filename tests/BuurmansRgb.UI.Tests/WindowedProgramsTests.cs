using System.ComponentModel;
using System.Diagnostics;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The list of "Kies uit wat nu draait" from a made-up list of processes, with the descriptions of
// their exe files by process id; a description that throws stands for a process that cannot be read.
public sealed class WindowedProgramsTests
{
    [Fact]
    public void Find_NamesEachProgramByTheDescriptionOfItsExe()
    {
        var programs = Find(
            [new(1, "olk", "Inbox - fiek - Outlook"), new(2, "TOTALCMD64", "Total Commander 11.03 - NOT REGISTERED")],
            new() { [1] = "Outlook", [2] = "Total Commander 64+" });

        Assert.Equal(["Outlook (olk.exe)", "Total Commander 64+ (TOTALCMD64.exe)"], Labels(programs));
        Assert.Equal(["olk.exe", "TOTALCMD64.exe"], programs.Select(program => program.ExeName));
    }

    // Without a description the title of its window names it, and without that its exe name, once.
    [Fact]
    public void Find_WithoutDescription_TakesTheTitleThenTheExeName()
    {
        var programs = Find([new(1, "mRemoteNG", "mRemoteNG - confCons.xml"), new(2, "game", ""), new(3, "tool", "  ")], new() { [2] = " " });

        Assert.Equal(["game.exe", "mRemoteNG - confCons.xml (mRemoteNG.exe)", "tool.exe"], Labels(programs));
        Assert.All(programs.Where(program => program.ExeName != "mRemoteNG.exe"), program => Assert.Equal(program.ExeName, program.Name));
    }

    // A process of another user, a protected one, one that ended or whose file is gone: the title
    // or the exe name instead, and the others still have theirs.
    [Fact]
    public void Find_WhenTheDescriptionCannotBeRead_FallsBack()
    {
        Exception[] failures =
        [
            new Win32Exception(5),
            new InvalidOperationException(),
            new UnauthorizedAccessException(),
            new FileNotFoundException(),
            new ArgumentException(),
        ];
        WindowedProcess[] processes =
        [
            new(0, "brave", ""),
            .. failures.Select((_, i) => new WindowedProcess(i + 1, $"locked{i + 1}", i % 2 == 0 ? $"Locked {i + 1}" : "")),
        ];

        var programs = WindowedPrograms.Find(processes, id => id == 0 ? "Brave Browser" : throw failures[id - 1]);

        Assert.Equal(
            ["Brave Browser (brave.exe)", "Locked 1 (locked1.exe)", "Locked 3 (locked3.exe)", "Locked 5 (locked5.exe)", "locked2.exe", "locked4.exe"],
            Labels(programs).Order(StringComparer.Ordinal));
    }

    // In the order of the names a user reads, not of the exe names.
    [Fact]
    public void Find_SortsByTheReadableName()
    {
        var programs = Find(
            [new(1, "olk", ""), new(2, "ms-teams", ""), new(3, "brave", ""), new(4, "devenv", "")],
            new() { [1] = "Outlook", [2] = "Microsoft Teams", [3] = "Brave Browser" });

        Assert.Equal(["Brave Browser", "devenv.exe", "Microsoft Teams", "Outlook"], programs.Select(program => program.Name));
    }

    // A program with more windows, or processes with one, is in the list once; one process that
    // describes it is enough, so the others are not read.
    [Fact]
    public void Find_GivesEachExeNameOnce()
    {
        var read = new List<int>();

        var programs = WindowedPrograms.Find(
            [new(1, "notepad++", "a.txt - Notepad++"), new(2, "NOTEPAD++", "b.txt - Notepad++"), new(3, "notepad++", "c.txt - Notepad++")],
            id =>
            {
                read.Add(id);
                return id == 1 ? throw new Win32Exception(5) : "Notepad++ : a free (GNU) source code editor";
            });

        var program = Assert.Single(programs);
        Assert.Equal("Notepad++ : a free (GNU) source code editor (notepad++.exe)", Label(program));
        Assert.Equal([1, 2], read);
    }

    // A name that only says the exe name again adds nothing.
    [Theory]
    [InlineData("notepad")]
    [InlineData("Notepad.exe")]
    [InlineData("  notepad ")]
    public void Find_ANameThatIsTheExeName_ShowsOnlyTheExeName(string description)
    {
        var program = Assert.Single(Find([new(1, "notepad", "")], new() { [1] = description }));

        Assert.Equal("notepad.exe", Label(program));
    }

    // Spaces around a description or title do not show in the list.
    [Fact]
    public void Find_TrimsTheName()
    {
        var programs = Find([new(1, "olk", ""), new(2, "game", "  ELDEN RING ")], new() { [1] = " Outlook  " });

        Assert.Equal(["ELDEN RING (game.exe)", "Outlook (olk.exe)"], Labels(programs));
    }

    // So a description that only says the exe name again leaves the turn to the title of the
    // window; a title that does the same leaves the exe name.
    [Theory]
    [InlineData("game")]
    [InlineData("GAME.exe")]
    public void Find_ADescriptionThatIsTheExeName_TakesTheTitle(string description)
    {
        var programs = Find([new(1, "game", "ELDEN RING"), new(2, "tool", "Tool")], new() { [1] = description, [2] = "tool" });

        Assert.Equal(["ELDEN RING (game.exe)", "tool.exe"], Labels(programs));
    }

    // The title of a document can be long; the list stays narrow and the field still gets the exe.
    [Fact]
    public void Find_CutsOffALongName()
    {
        var title = "Een heel lang document met een titel die maar doorgaat en doorgaat.docx - Word";

        var program = Assert.Single(Find([new(1, "WINWORD", title)], new()));

        Assert.InRange(program.Name.Length, WindowedPrograms.MaxNameLength - 2, WindowedPrograms.MaxNameLength);
        Assert.StartsWith(title[..40], program.Name, StringComparison.Ordinal);
        Assert.EndsWith("…", program.Name, StringComparison.Ordinal);
        Assert.Equal("WINWORD.exe", program.ExeName);
    }

    // An emoji where the name is cut off goes whole, so no half of it shows as a box.
    [Fact]
    public void Find_CutsOffALongNameBeforeAnEmoji()
    {
        var title = new string('a', WindowedPrograms.MaxNameLength - 2) + "🎮 en nog wat meer";

        var program = Assert.Single(Find([new(1, "game", title)], new()));

        Assert.Equal(new string('a', WindowedPrograms.MaxNameLength - 2) + "…", program.Name);
        Assert.DoesNotContain(program.Name, char.IsSurrogate);
    }

    // Parts of Windows without an app of their own are left out, in any case, without reading
    // their file; Instellingen and Verkenner, which a user opens, stay.
    [Theory]
    [InlineData("ApplicationFrameHost")]
    [InlineData("TextInputHost")]
    [InlineData("textinputhost")]
    [InlineData("ShellExperienceHost")]
    [InlineData("StartMenuExperienceHost")]
    [InlineData("SearchHost")]
    [InlineData("SearchApp")]
    [InlineData("SearchUI")]
    [InlineData("ShellHost")]
    [InlineData("LockApp")]
    public void Find_LeavesOutPartsOfWindows(string part)
    {
        var read = new List<int>();

        var programs = WindowedPrograms.Find(
            [new(1, part, "Windows Input Experience"), new(2, "SystemSettings", "Instellingen"), new(3, "explorer", "Downloads")],
            id =>
            {
                read.Add(id);
                return null;
            });

        Assert.Equal(["SystemSettings.exe", "explorer.exe"], programs.Select(program => program.ExeName).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(1, read);
    }

    // Each part of Windows that the engine lets pass by in the foreground, also one added later.
    [Fact]
    public void Find_LeavesOutEachShellPart()
    {
        var programs = WindowedPrograms.Find(
            [.. ProgramNames.ShellParts.Select((part, index) => new WindowedProcess(index + 1, part, "Windows")), new(99, "game", "Game")],
            _ => null);

        Assert.Equal(["game.exe"], programs.Select(program => program.ExeName));
    }

    // A name that is no exe name the field takes, such as an empty one, is left out.
    [Fact]
    public void Find_LeavesOutNamesThatAreNoProgram()
    {
        var programs = Find([new(1, "", "Zonder naam"), new(2, "bad|name", ""), new(3, "game", "")], new());

        Assert.Equal(["game.exe"], programs.Select(program => program.ExeName));
    }

    // The description of the test process, as Windows reads it from its file; the idle process,
    // which no one can open, has none. Only this own process: what else runs differs per pc.
    [Fact]
    public void SystemProcesses_ReadsTheDescriptionOfAnExe()
    {
        var expected = FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).FileDescription;

        Assert.Equal(expected, SystemProcesses.Description(Environment.ProcessId));
        Assert.Null(SystemProcesses.Description(0));
    }

    // As the list shows them (RunningProgramItem).
    private static IEnumerable<string> Labels(IEnumerable<RunningProgram> programs) => programs.Select(Label);

    private static string Label(RunningProgram program) => new RunningProgramItem(program).Label;

    private static IReadOnlyList<RunningProgram> Find(WindowedProcess[] processes, Dictionary<int, string?> descriptions) =>
        WindowedPrograms.Find(processes, id => descriptions.GetValueOrDefault(id));
}
