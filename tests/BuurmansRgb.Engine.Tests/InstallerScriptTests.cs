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
    public void Uninstall_RemovesNoSettingsThroughTheElevatedProfile()
    {
        // {userappdata} is that of whoever answered UAC; OriginalUserAppData uses it only for the
        // user who started the uninstall.
        var deletes = UninstallCode().Where(line => line.Contains("DelTree(", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(deletes);
        Assert.All(deletes, line => Assert.DoesNotContain("{userappdata}", line, StringComparison.OrdinalIgnoreCase));
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
