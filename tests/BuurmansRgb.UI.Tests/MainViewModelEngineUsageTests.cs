using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// When the memory and CPU of the engine are read: only on Instellingen, with the window open and
// the engine connected; a fake meter, so never a process of this pc.
public sealed class MainViewModelEngineUsageTests
{
    private readonly FakeEngineConnector _connector = new();
    private readonly CountingMeter _meter = new();

    [Fact]
    public async Task OtherPages_ReadNothing()
    {
        using var viewModel = await ConnectAsync();

        foreach (var page in new[] { AppPage.Lamps, AppPage.Color, AppPage.Scenes })
        {
            viewModel.Navigation.GoTo(page);
            Assert.False(viewModel.EngineUsage.IsMeasuring);
        }

        Assert.Empty(_meter.Reads);
    }

    // Instellingen reads the engine this connection gave, and leaving it stops.
    [Fact]
    public async Task Settings_ReadsTheEngineUntilThePageGoes()
    {
        using var viewModel = await ConnectAsync(api => api.ProcessId = 777);

        viewModel.Navigation.GoTo(AppPage.Settings);
        Assert.True(viewModel.EngineUsage.IsMeasuring);
        Assert.Equal([777], _meter.Reads);

        viewModel.Navigation.GoTo(AppPage.Lamps);
        Assert.False(viewModel.EngineUsage.IsMeasuring);
    }

    [Fact]
    public async Task Minimized_Stops()
    {
        using var viewModel = await ConnectAsync();
        viewModel.Navigation.GoTo(AppPage.Settings);

        viewModel.IsWindowMinimized = true;
        Assert.False(viewModel.EngineUsage.IsMeasuring);

        viewModel.IsWindowMinimized = false;
        Assert.True(viewModel.EngineUsage.IsMeasuring);
    }

    [Fact]
    public async Task ConnectionLost_Stops()
    {
        using var viewModel = await ConnectAsync();
        viewModel.Navigation.GoTo(AppPage.Settings);

        _connector.Connection.Disconnect();

        Assert.False(viewModel.EngineUsage.IsMeasuring);
        Assert.Null(viewModel.EngineUsage.ProcessId);
    }

    // Closing the app (App disposes the view model) stops the timer and lets go of the process.
    [Fact]
    public async Task Dispose_Stops()
    {
        var viewModel = await ConnectAsync();
        viewModel.Navigation.GoTo(AppPage.Settings);

        viewModel.Dispose();

        Assert.False(viewModel.EngineUsage.IsMeasuring);
        Assert.True(_meter.IsDisposed);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, api => arrange?.Invoke(api)), processMeter: _meter);
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }

    private sealed class CountingMeter : IProcessMeter, IDisposable
    {
        public List<int> Reads { get; } = [];

        public bool IsDisposed { get; private set; }

        public ProcessReading? Read(int processId)
        {
            lock (Reads)
            {
                Reads.Add(processId);
            }

            return new ProcessReading(TimeSpan.Zero, 1024 * 1024);
        }

        public void Dispose() => IsDisposed = true;
    }
}
