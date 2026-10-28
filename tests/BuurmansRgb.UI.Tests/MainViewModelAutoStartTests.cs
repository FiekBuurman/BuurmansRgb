using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// "Starten met Windows" on Instellingen, through a fake engine: never the registry of this pc.
public sealed class MainViewModelAutoStartTests
{
    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public void AutoStart_IsOffUntilTheEngineSaid()
    {
        using var viewModel = new MainViewModel(_connector);

        Assert.False(viewModel.AutoStart.IsOn);
    }

    [Fact]
    public async Task Connect_ShowsAutoStartWithoutSendingIt()
    {
        using var viewModel = await ConnectAsync(api => api.AutoStart = true);

        Assert.True(viewModel.AutoStart.IsOn);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task Switched_SendsItToTheEngine()
    {
        using var viewModel = await ConnectAsync();

        viewModel.AutoStart.IsOn = true;
        viewModel.AutoStart.IsOn = false;

        Assert.Equal(["autostart True", "autostart False"], Api.Calls);
        Assert.False(Api.AutoStart);
        Assert.Null(viewModel.StatusMessage);
    }

    // As the tray menu does: Windows may not let the registry change. The switch shows what the
    // engine has again, and that goes not back to the engine.
    [Fact]
    public async Task Switched_WhenWindowsRefuses_SaysSoAndShowsTheEngineAgain()
    {
        using var viewModel = await ConnectAsync(api => api.AutoStartChangeable = false);

        viewModel.AutoStart.IsOn = true;

        Assert.False(viewModel.AutoStart.IsOn);
        Assert.Equal(Strings.AutoStartFailed, viewModel.StatusMessage);
        Assert.Equal(["autostart True"], Api.Calls);
    }

    // The tray menu changed it: the engine tells through OptionsChanged, and the switch follows.
    [Fact]
    public async Task ChangedInTheTrayMenu_FollowsWithoutSendingItBack()
    {
        using var viewModel = await ConnectAsync();

        Api.AutoStart = true;
        Api.RaiseOptionsChanged();
        Assert.True(viewModel.AutoStart.IsOn);

        Api.AutoStart = false;
        Api.RaiseOptionsChanged();
        Assert.False(viewModel.AutoStart.IsOn);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task Switched_WithoutConnection_SendsNothing()
    {
        using var viewModel = await ConnectAsync();
        _connector.Connection.Disconnect();

        viewModel.AutoStart.IsOn = true;

        Assert.Empty(Api.Calls);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api => arrange?.Invoke(api)));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
