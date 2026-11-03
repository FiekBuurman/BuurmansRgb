using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The other RGB software the engine found, from connecting on, in the status line.
public sealed class MainViewModelOtherRgbSoftwareTests
{
    private static readonly OtherRgbSoftware SignalRgb = new("signalrgb", "SignalRGB");
    private static readonly OtherRgbSoftware Icue = new("icue", "Corsair iCUE");

    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task Connect_NoneRuns_ShowsNoMessage()
    {
        var viewModel = await ConnectAsync(_ => { });

        Assert.Empty(viewModel.OtherRgbSoftware.Programs);
        Assert.False(viewModel.StatusLine.IsShown);
        Assert.True(Api.HasOtherRgbSoftwareSubscribers);
    }

    [Fact]
    public async Task Connect_ShowsWhatTheEngineFoundAndFollowsIt()
    {
        var viewModel = await ConnectAsync(api => api.OtherRgbSoftware = [SignalRgb]);
        Assert.Equal("SignalRGB draait ook en kan je kleuren overschrijven.", viewModel.StatusLine.Current?.Text);

        Api.OtherRgbSoftware = [SignalRgb, Icue];
        Api.RaiseOtherRgbSoftwareChanged();

        Assert.Equal([SignalRgb, Icue], viewModel.OtherRgbSoftware.Programs);
        Assert.Equal(StatusMessageKind.OtherRgbSoftware, Assert.Single(viewModel.StatusLine.Others).Kind);
    }

    [Fact]
    public async Task NietMeerMelden_SendsItToTheEngine()
    {
        var viewModel = await ConnectAsync(api => api.OtherRgbSoftware = [SignalRgb]);

        viewModel.StatusLine.Current!.DismissCommand!.Execute(null);

        Assert.Contains("dismiss signalrgb", Api.Calls);
        Assert.Empty(viewModel.OtherRgbSoftware.Programs);
        Assert.False(viewModel.StatusLine.IsShown);
    }

    // "Uitleg" always, as it unfolds in the app; the link under it only with a guide to open
    // (AppInfoViewModel.GuideAddress, none yet).
    [Fact]
    public async Task Uitleg_AlwaysAndTheLinkFollowsTheGuideOfTheApp()
    {
        var viewModel = await ConnectAsync(api => api.OtherRgbSoftware = [SignalRgb]);

        Assert.True(viewModel.StatusLine.Current?.HasExplanation);
        Assert.Equal(!string.IsNullOrWhiteSpace(AppInfoViewModel.GuideAddress), viewModel.StatusLine.Current?.HasExplanationLink);
        Assert.Equal(Strings.OtherRgbSoftwareDismiss, viewModel.StatusLine.Current?.DismissText);
    }

    [Fact]
    public async Task Disconnected_ShowsNoneAndStopsFollowing()
    {
        var viewModel = await ConnectAsync(api => api.OtherRgbSoftware = [SignalRgb]);

        _connector.Connection.Disconnect();

        Assert.Empty(viewModel.OtherRgbSoftware.Programs);
        Assert.False(Api.HasOtherRgbSoftwareSubscribers);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi> arrange)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, arrange));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
