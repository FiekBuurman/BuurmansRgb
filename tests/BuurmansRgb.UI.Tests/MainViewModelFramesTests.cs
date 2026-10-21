using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The engine sends frames for the preview only while a page shows what the lamps do now.
public sealed class MainViewModelFramesTests
{
    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    // The engine starts with frames, so on Lampen there is nothing to say.
    [Fact]
    public async Task Connect_OnLampen_SaysNothing()
    {
        await ConnectAsync();

        Assert.Null(Api.FramesWanted);
    }

    [Theory]
    [InlineData(AppPage.Scenes)]
    [InlineData(AppPage.Neighbours)]
    [InlineData(AppPage.Settings)]
    public async Task PageWithoutPreview_WantsNoFrames(AppPage page)
    {
        var viewModel = await ConnectAsync();

        viewModel.Navigation.GoTo(page);

        Assert.False(Api.FramesWanted);
    }

    [Fact]
    public async Task BackToKleur_WantsFramesAgain()
    {
        var viewModel = await ConnectAsync();
        viewModel.Navigation.GoTo(AppPage.Scenes);

        viewModel.Navigation.GoTo(AppPage.Color);

        Assert.True(Api.FramesWanted);
    }

    [Fact]
    public async Task Minimized_WantsNoFrames_AndRestoredWantsThemAgain()
    {
        var viewModel = await ConnectAsync();

        viewModel.IsWindowMinimized = true;
        Assert.False(Api.FramesWanted);

        viewModel.IsWindowMinimized = false;
        Assert.True(Api.FramesWanted);
    }

    // A new connection starts with frames again, so it is told when the page has no preview.
    [Fact]
    public async Task Reconnect_OnAPageWithoutPreview_SaysSoAgain()
    {
        var viewModel = await ConnectAsync();
        viewModel.Navigation.GoTo(AppPage.Settings);

        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.False(Api.FramesWanted);
    }

    // All devices until said otherwise, as Lampen needs them.
    [Fact]
    public async Task Connect_OnLampen_AsksForNoSingleDevice()
    {
        await ConnectAsync();

        Assert.Empty(Api.FrameDevicesCalls);
    }

    [Fact]
    public async Task Kleur_WithOneLamp_AsksOnlyForThatLamp()
    {
        var viewModel = await ConnectAsync();
        viewModel.Navigation.GoTo(AppPage.Color);

        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        Assert.Equal(["fan"], Api.FrameDevices!);
    }

    [Fact]
    public async Task Kleur_WholeHouse_AsksForAllAgain()
    {
        var viewModel = await ConnectAsync();
        viewModel.Navigation.GoTo(AppPage.Color);
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId is null);

        Assert.Null(Api.FrameDevices);
    }

    [Fact]
    public async Task BackToLampen_AsksForAllAgain()
    {
        var viewModel = await ConnectAsync();
        viewModel.Navigation.GoTo(AppPage.Color);
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        viewModel.Navigation.GoTo(AppPage.Lamps);

        Assert.Null(Api.FrameDevices);
        Assert.Equal(2, Api.FrameDevicesCalls.Count);
    }

    // A new connection starts with all devices again, so it is told about the lamp once more.
    [Fact]
    public async Task Reconnect_OnKleurWithOneLamp_AsksForItAgain()
    {
        var viewModel = await ConnectAsync();
        viewModel.Navigation.GoTo(AppPage.Color);
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        await viewModel.ConnectCommand.ExecuteAsync(null);

        Assert.Equal(["fan"], Api.FrameDevices!);
    }

    private async Task<MainViewModel> ConnectAsync()
    {
        var viewModel = new MainViewModel(_connector);
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
