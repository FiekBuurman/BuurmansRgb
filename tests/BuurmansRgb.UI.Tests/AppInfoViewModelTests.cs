using System.ComponentModel;
using System.Reflection;
using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The bottom of Instellingen, with a fake open: no test opens a file or a browser on this pc.
public sealed class AppInfoViewModelTests
{
    private const string LogPath = @"C:\Users\buurman\AppData\Roaming\BuurmansRgb\logs\engine.log";

    private readonly List<string> _opened = [];

    public static TheoryData<Exception> OpenErrors => [new Win32Exception(2), new IOException(), new InvalidOperationException()];

    [Fact]
    public void OpenLog_OpensTheLogOfTheEngine()
    {
        var appInfo = new AppInfoViewModel(_opened.Add, LogPath, "1.2.0");
        var failures = new List<string>();
        appInfo.Failed += (_, message) => failures.Add(message);

        appInfo.OpenLogCommand.Execute(null);

        Assert.Equal([LogPath], _opened);
        Assert.Empty(failures);
    }

    // A log that is not there (yet), or no app for .log files: a message, as in the tray menu.
    [Theory]
    [MemberData(nameof(OpenErrors))]
    public void OpenLog_WhenWindowsCannotOpenIt_SaysSo(Exception error)
    {
        var appInfo = new AppInfoViewModel(_ => throw error, LogPath, "1.2.0");
        var failures = new List<string>();
        appInfo.Failed += (_, message) => failures.Add(message);

        appInfo.OpenLogCommand.Execute(null);

        Assert.Equal([Strings.LogOpenFailed], failures);
    }

    [Fact]
    public void OpenGuide_OpensTheGuideOnGitHub()
    {
        var appInfo = new AppInfoViewModel(_opened.Add, LogPath, "1.2.0");

        appInfo.OpenGuideCommand.Execute(null);

        Assert.Equal([AppInfoViewModel.GuideAddress], _opened);
        Assert.StartsWith("https://github.com/", AppInfoViewModel.GuideAddress, StringComparison.Ordinal);
        Assert.EndsWith("/docs/handleiding.md", AppInfoViewModel.GuideAddress, StringComparison.Ordinal);
    }

    // No browser, or Windows refuses: a message instead of a crash.
    [Theory]
    [MemberData(nameof(OpenErrors))]
    public void OpenGuide_WhenWindowsCannotOpenIt_SaysSo(Exception error)
    {
        var appInfo = new AppInfoViewModel(_ => throw error, LogPath, "1.2.0");
        var failures = new List<string>();
        appInfo.Failed += (_, message) => failures.Add(message);

        appInfo.OpenGuideCommand.Execute(null);

        Assert.Equal([Strings.GuideOpenFailed], failures);
    }

    // The build adds the commit after a +; the user only needs the version.
    [Theory]
    [InlineData("1.2.0", "buurmansRgb 1.2.0")]
    [InlineData("1.2.0+f2a4d2cce4ff2c55100503eed3399dc53615c942", "buurmansRgb 1.2.0")]
    [InlineData("1.3.0-beta.1+abc", "buurmansRgb 1.3.0-beta.1")]
    public void VersionText_IsTheVersionWithoutTheCommit(string version, string text) =>
        Assert.Equal(text, new AppInfoViewModel(_opened.Add, LogPath, version).VersionText);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void VersionText_WithoutVersion_SaysItIsUnknown(string? version) =>
        Assert.Equal($"buurmansRgb {Strings.VersionUnknown}", new AppInfoViewModel(_opened.Add, LogPath, version).VersionText);

    // The version of this build of the app.
    [Fact]
    public void MainViewModel_ShowsTheVersionOfTheApp()
    {
        using var viewModel = new MainViewModel(new FakeEngineConnector(), open: _opened.Add);
        var version = typeof(MainViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.Equal($"buurmansRgb {version.Split('+')[0]}", viewModel.AppInfo.VersionText);
        Assert.Matches(@"^buurmansRgb \d+\.\d+\.\d+", viewModel.AppInfo.VersionText);
    }

    // The log the engine writes, also while the engine does not run: no call goes to it.
    [Fact]
    public void MainViewModel_OpensTheLogWhereTheEngineWritesIt()
    {
        var connector = new FakeEngineConnector();
        using var viewModel = new MainViewModel(connector, open: _opened.Add);

        viewModel.AppInfo.OpenLogCommand.Execute(null);

        Assert.Equal([FileLog.PathOf(FileLog.EngineName)], _opened);
        Assert.Empty(connector.Connection.FakeApi.Calls);
    }

    [Fact]
    public void MainViewModel_ShowsAFailureInTheStatusLine()
    {
        using var viewModel = new MainViewModel(new FakeEngineConnector(), open: _ => throw new Win32Exception(2));

        viewModel.AppInfo.OpenLogCommand.Execute(null);

        Assert.Equal(Strings.LogOpenFailed, viewModel.StatusMessage);
    }
}
