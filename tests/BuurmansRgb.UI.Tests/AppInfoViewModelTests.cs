using System.ComponentModel;
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
        var appInfo = new AppInfoViewModel(_opened.Add, LogPath);
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
        var appInfo = new AppInfoViewModel(_ => throw error, LogPath);
        var failures = new List<string>();
        appInfo.Failed += (_, message) => failures.Add(message);

        appInfo.OpenLogCommand.Execute(null);

        Assert.Equal([Strings.LogOpenFailed], failures);
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
