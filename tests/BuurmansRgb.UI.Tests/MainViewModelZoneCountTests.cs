using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// Counting the LEDs of a zone from the preview on Kleur, against the fake engine, which reports
// the count as the engine does.
public sealed class MainViewModelZoneCountTests
{
    // A board with LEDs of its own and an addressable header of up to 60 LEDs with a fan on it.
    private static readonly DeviceInfo Board = new("board", "Board", DeviceType.Motherboard, 5,
        [RgbZone.Fixed("Mainboard", 2), new RgbZone("ARGB 1", 3, 0, 60)]) { Source = DeviceSource.Hardware };

    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task CountLeds_StartsTheCountAndShowsIt()
    {
        var viewModel = await ConnectAsync();

        Header(viewModel).CountLedsCommand.Execute(null);

        Assert.Equal(["count board 1 1"], Api.Calls);
        await Eventually.TrueAsync(() => Header(viewModel).Count is not null);
        Assert.Equal((1, 60, true), (Header(viewModel).Count!.Led, Header(viewModel).Count!.LedCount, Header(viewModel).Count!.Running));
    }

    [Fact]
    public async Task EngineReportsAStep_TheCounterFollows()
    {
        var viewModel = await ConnectAsync();
        Header(viewModel).CountLedsCommand.Execute(null);
        await Eventually.TrueAsync(() => Header(viewModel).Count is not null);

        Api.RaiseZoneCount(new ZoneCountInfo("board", 1, 2, 60, 1, true));

        await Eventually.TrueAsync(() => Header(viewModel).Count?.LedText == "LED 2");
    }

    [Fact]
    public async Task PausePreviousAndPace_GoToTheEngine()
    {
        var viewModel = await ConnectAsync();
        Header(viewModel).CountLedsCommand.Execute(null);
        await Eventually.TrueAsync(() => Header(viewModel).Count is not null);
        var count = Header(viewModel).Count!;

        count.NextCommand.Execute(null);
        count.PaceOptions[1].IsChecked = true;
        count.PauseOrResumeCommand.Execute(null);

        await Eventually.TrueAsync(() => Api.Calls.Count == 4);
        Assert.Equal(["count board 1 1", "count board 1 led 2 pause", "count board 1 pace 2", "count board 1 run"], Api.Calls);
        Assert.Equal(new ZoneCountInfo("board", 1, 2, 60, 2, true), Api.ZoneCount);
    }

    // "Dit is de laatste" sets the length the existing way, and the engine ends the count with it.
    [Fact]
    public async Task Keep_ResizesTheZoneAndTheCountEnds()
    {
        var viewModel = await ConnectAsync();
        Header(viewModel).CountLedsCommand.Execute(null);
        await Eventually.TrueAsync(() => Header(viewModel).Count is not null);
        Api.RaiseZoneCount(new ZoneCountInfo("board", 1, 37, 60, 1, false));
        await Eventually.TrueAsync(() => Header(viewModel).Count?.Led == 37);

        Header(viewModel).Count!.KeepCommand.Execute(null);

        await Eventually.TrueAsync(() => Api.Calls.Contains("resize board 1 37"));
        await Eventually.TrueAsync(() => Header(viewModel).Count is null);
    }

    [Fact]
    public async Task Stop_EndsTheCount()
    {
        var viewModel = await ConnectAsync();
        Header(viewModel).CountLedsCommand.Execute(null);
        await Eventually.TrueAsync(() => Header(viewModel).Count is not null);

        Header(viewModel).Count!.StopCommand.Execute(null);

        Assert.Null(Header(viewModel).Count);
        await Eventually.TrueAsync(() => Api.Calls.Contains("count board 1 stop"));
        Assert.False(Api.ZoneCount.IsCounting);
    }

    // The count shows only on Kleur, so leaving it stops the count.
    [Fact]
    public async Task LeavingKleur_StopsTheCount()
    {
        var viewModel = await ConnectAsync();
        Header(viewModel).CountLedsCommand.Execute(null);
        await Eventually.TrueAsync(() => Header(viewModel).Count is not null);

        viewModel.Navigation.GoTo(AppPage.Scenes);

        await Eventually.TrueAsync(() => Api.Calls.Contains("count board 1 stop"));
        Assert.Null(viewModel.Preview.Count);
    }

    [Fact]
    public async Task Paused_GreysOutCountLeds()
    {
        var viewModel = await ConnectAsync();

        Api.RaisePause(PauseReasons.Locked);
        Assert.False(Header(viewModel).CountLedsCommand.CanExecute(null));

        Api.RaisePause(PauseReasons.None);
        Assert.True(Header(viewModel).CountLedsCommand.CanExecute(null));
    }

    // The engine ends the count of a connection that went; the app shows none then.
    [Fact]
    public async Task ConnectionLost_EndsTheCount()
    {
        var viewModel = await ConnectAsync();
        Header(viewModel).CountLedsCommand.Execute(null);
        await Eventually.TrueAsync(() => Header(viewModel).Count is not null);

        _connector.Connection.Disconnect();

        Assert.Null(viewModel.Preview.Count);
        Assert.False(Api.HasZoneCountSubscribers);
    }

    private static ZonePreviewViewModel Header(MainViewModel viewModel) => viewModel.Preview.Devices.Single().Zones[1];

    private async Task<MainViewModel> ConnectAsync()
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api => api.Devices = [Board]));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        viewModel.Navigation.GoTo(AppPage.Color);
        return viewModel;
    }
}
