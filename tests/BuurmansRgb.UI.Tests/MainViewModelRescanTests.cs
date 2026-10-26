using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// "Verversen" on Apparaten against the engine.
public sealed class MainViewModelRescanTests
{
    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    // The headset that came back is in the list by the time the button says "Verversen" again.
    [Fact]
    public async Task Rescan_SearchesUntilTheEngineAnswersWithTheNewList()
    {
        var viewModel = await ConnectAsync();
        var gate = new TaskCompletionSource();
        Api.RescanGate = gate.Task;

        viewModel.DeviceRescan.RescanCommand.Execute(null);

        Assert.True(viewModel.DeviceRescan.IsSearching);
        Assert.Equal(["rescan"], Api.Calls);
        Api.Devices = [.. Api.Devices, TestDevices.Create("headset", "Koptelefoon", DeviceType.Headset, 2)];
        gate.SetResult();
        await Eventually.TrueAsync(() => !viewModel.DeviceRescan.IsSearching);
        Assert.Contains(viewModel.Lamps.Lamps, lamp => lamp.DeviceId == "headset");
        Assert.False(viewModel.DeviceRescan.HasMessage);
    }

    [Fact]
    public async Task Rescan_OpenRgbNotReached_SaysSo()
    {
        var viewModel = await ConnectAsync();
        Api.RescanReaches = false;

        viewModel.DeviceRescan.RescanCommand.Execute(null);

        await Eventually.TrueAsync(() => !viewModel.DeviceRescan.IsSearching);
        Assert.Equal(Strings.RescanOpenRgbUnreachable, viewModel.DeviceRescan.Message);
    }

    // The engine rejected the call: its own message tells that, the button just ends.
    [Fact]
    public async Task Rescan_CallFails_EndsWithoutTheOpenRgbMessage()
    {
        var viewModel = await ConnectAsync();
        Api.SetException = new InvalidOperationException("Broken");

        viewModel.DeviceRescan.RescanCommand.Execute(null);

        await Eventually.TrueAsync(() => !viewModel.DeviceRescan.IsSearching);
        Assert.False(viewModel.DeviceRescan.HasMessage);
    }

    [Fact]
    public async Task Rescan_SecondClickWhileSearching_SendsOneCall()
    {
        var viewModel = await ConnectAsync();
        var gate = new TaskCompletionSource();
        Api.RescanGate = gate.Task;

        viewModel.DeviceRescan.RescanCommand.Execute(null);
        viewModel.DeviceRescan.RescanCommand.Execute(null);
        gate.SetResult();

        await Eventually.TrueAsync(() => !viewModel.DeviceRescan.IsSearching);
        Assert.Equal(["rescan"], Api.Calls);
    }

    [Fact]
    public async Task ConnectionLost_EndsTheSearch()
    {
        var viewModel = await ConnectAsync();
        Api.RescanGate = new TaskCompletionSource().Task;
        viewModel.DeviceRescan.RescanCommand.Execute(null);

        _connector.Connection.Disconnect();

        Assert.False(viewModel.DeviceRescan.IsSearching);
        Assert.False(viewModel.DeviceRescan.HasMessage);
    }

    private async Task<MainViewModel> ConnectAsync()
    {
        var viewModel = new MainViewModel(_connector);
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
