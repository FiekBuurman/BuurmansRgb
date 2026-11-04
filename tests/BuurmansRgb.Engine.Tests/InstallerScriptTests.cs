using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.AutoStart;

namespace BuurmansRgb.Engine.Tests;

// Checks on the text of installer\BuurmansRgb.iss, which has no tests of its own: the uninstall
// stops only the apps of the user who started it, the engine first with --exit, and removes no
// settings of another user. Whether it works is the Windows Sandbox test (docs/ontwikkelaars.md).
public sealed class InstallerScriptTests
{
    // A call of taskkill, not a comment or a log line about it.
    private const string TaskKill = @"{sys}\taskkill.exe";

    private static readonly string[] Lines = File.ReadAllLines(ScriptPath());

    [Fact]
    public void ExitArgument_IsTheOneOfTheEngine()
    {
        Assert.Contains($"#define ExitArgument \"{EngineOptions.ExitArgument}\"", Lines);
    }

    // The installer removes %AppData%\<AppName> and the Run value <AppName> of the engine.
    [Fact]
    public void AppName_IsTheNameOfTheDataFolder()
    {
        Assert.Contains($"#define AppName \"{DataFolder.Name}\"", Lines);
    }

    [Fact]
    public void DataFolderName_IsTheNameOfTheAutoStart()
    {
        Assert.Equal(DataFolder.Name, RegistryAutoStart.ValueName);
    }

    [Fact]
    public void FirstRunArgument_IsTheOneOfTheEngine()
    {
        Assert.Contains($"#define FirstRunArgument \"{EngineOptions.FirstRunArgument}\"", Lines);
        var run = Array.FindIndex(Lines, line => line.Trim().Equals("[Run]", StringComparison.OrdinalIgnoreCase));
        Assert.True(run >= 0, "[Run] not found in the installer script.");
        Assert.Contains(Lines.Skip(run + 1).TakeWhile(line => !line.TrimStart().StartsWith('[')), line => line.Contains("Parameters: \"{#FirstRunArgument}\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Uninstall_HasNoUninstallRunSection()
    {
        // [UninstallRun] cannot filter on the user who started the uninstall.
        Assert.DoesNotContain(Lines, line => line.Trim().Equals("[UninstallRun]", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Uninstall_KillsOnlyProcessesOfOneUser()
    {
        var uninstall = UninstallCode();
        var kills = uninstall.Where(line => line.Contains(TaskKill, StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.NotEmpty(kills);
        Assert.All(kills, line => Assert.Contains("/FI \"USERNAME eq ", line, StringComparison.Ordinal));
    }

    [Fact]
    public void Uninstall_AsksTheEngineToExitBeforeKillingIt()
    {
        var uninstall = UninstallCode();
        var exit = uninstall.FindIndex(line => line.Contains("'{#ExitArgument}'", StringComparison.Ordinal));
        var kill = uninstall.FindIndex(line => line.Contains(TaskKill, StringComparison.OrdinalIgnoreCase));

        Assert.InRange(exit, 0, kill - 1);
    }

    [Fact]
    public void Uninstall_AsksTheEngineToExitAlsoWhenTheUserIsNotKnown()
    {
        // The signal only opens for its owner, so --exit is as safe without the SID as with it.
        var uninstall = UninstallCode();
        var exit = uninstall.FindIndex(line => line.Contains("'{#ExitArgument}'", StringComparison.Ordinal));
        var condition = uninstall.FindLastIndex(exit, line => line.TrimStart().StartsWith("if ", StringComparison.Ordinal));

        Assert.InRange(condition, 0, exit - 1);
        Assert.Contains("(OriginalUserSid = '') or OriginalUserIsCurrent", uninstall[condition], StringComparison.Ordinal);
    }

    [Fact]
    public void Uninstall_RemovesNoSettingsThroughTheElevatedProfile()
    {
        // {userappdata} is that of whoever answered UAC; OriginalUserAppData uses it only for the
        // user who started the uninstall.
        var deletes = UninstallCode().Where(line => line.Contains("DelTree(", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(deletes);
        Assert.All(deletes, line => Assert.DoesNotContain("{userappdata}", line, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Uninstall_TrustsTheAppDataOfAnotherUserOnlyInsideHisProfile()
    {
        // The user can set his Shell Folders keys himself; ProfileList only an admin.
        var function = FunctionCode("function OriginalUserAppData(");
        var profile = function.FindIndex(line => line.Contains("'ProfileImagePath'", StringComparison.Ordinal));
        var shellFolders = function.FindIndex(line => line.Contains("RegQueryStringValue(HKU,", StringComparison.Ordinal));
        var check = function.FindIndex(line => line.Contains("not IsInsideFolder(Dir, Profile)", StringComparison.Ordinal));

        Assert.InRange(profile, 0, shellFolders - 1);
        Assert.True(check > shellFolders, "OriginalUserAppData does not check that AppData lies inside the profile.");
    }

    // From the line that starts with header to the next "end;" at the start of a line.
    private static List<string> FunctionCode(string header)
    {
        var start = Array.FindIndex(Lines, line => line.StartsWith(header, StringComparison.Ordinal));
        Assert.True(start >= 0, $"{header} not found in the installer script.");
        var end = Array.FindIndex(Lines, start, line => line == "end;");
        return Lines[start..(end + 1)].ToList();
    }

    // From InitializeUninstall to the end: the code that only Uninstall runs.
    private static List<string> UninstallCode()
    {
        var start = Array.FindIndex(Lines, line => line.StartsWith("function InitializeUninstall", StringComparison.Ordinal));
        Assert.True(start >= 0, "InitializeUninstall not found in the installer script.");
        return Lines[start..].ToList();
    }

    private static string ScriptPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BuurmansRgb.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(Assert.IsType<DirectoryInfo>(directory).FullName, "installer", "BuurmansRgb.iss");
    }
}
