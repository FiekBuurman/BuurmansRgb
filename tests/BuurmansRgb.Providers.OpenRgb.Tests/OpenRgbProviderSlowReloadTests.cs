using BuurmansRgb.Core;
using OpenRgbDeviceType = OpenRGB.NET.DeviceType;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// OpenRGB that takes its time to give its list after a device list change, as while it searches
// with G HUB and Gigabyte Control Center open (thuistest 24-11-2026, F2): the connection stays as
// long as it is open, and only one that closes counts as lost.
public sealed class OpenRgbProviderSlowReloadTests : IDisposable
{
    private static readonly OpenRgbDeviceData Strip = TestDevices.Create(index: 0, name: "Strip", location: "HID: strip");

    private static readonly OpenRgbDeviceData Headset =
        TestDevices.Create(index: 1, name: "Headset", type: OpenRgbDeviceType.Headset, location: "HID: headset", ledCount: 2);

    private readonly FakeOpenRgbConnector _connector = new() { Devices = [Strip] };
    private readonly TimerCountingTimeProvider _time = new();
    private readonly OpenRgbDeviceProvider _provider;
    private readonly List<ServerStatus> _statuses = [];

    public OpenRgbProviderSlowReloadTests()
    {
        _provider = new OpenRgbDeviceProvider(_connector, _time, OpenRgbDeviceProvider.RetryDelay);
        _provider.StatusChanged += (_, status) =>
        {
            lock (_statuses)
            {
                _statuses.Add(status);
            }
        };
    }

    public void Dispose() => _provider.Dispose();

    [Fact]
    public void ReloadTimeout_IsLongerThanTheLoadTimeout() =>
        Assert.True(OpenRgbDeviceProvider.ReloadTimeout > OpenRgbDeviceProvider.LoadTimeout * 2);

    // Connecting has no list to keep, so only LoadTimeout: a list that never comes ends the new
    // connection, and the next try follows after RetryDelay.
    [Fact]
    public async Task Connect_NoListWithinTheLoadTimeout_DisposesAndTriesAgain()
    {
        _connector.DevicesAnswered = new TaskCompletionSource().Task;
        var timers = _time.Timers;
        await _provider.DiscoverAsync(Ct);

        await _time.AdvanceAfterTimerAsync(timers, OpenRgbDeviceProvider.LoadTimeout);
        await _provider.WhenIdleAsync();

        Assert.True(Assert.Single(_connector.Connections).IsDisposed);
        Assert.Equal(ServerState.Failed, _provider.Status.State);
        Assert.Empty(await _provider.DiscoverAsync(Ct));

        _connector.DevicesAnswered = Task.CompletedTask;
        await RetryAsync(expectedConnects: 2);
        Assert.Single(await _provider.DiscoverAsync(Ct));
        Assert.Equal(new ServerStatus(ServerState.Connected, 1), _provider.Status);
    }

    [Fact]
    public async Task Reload_LongerThanTheLoadTimeout_KeepsTheConnectionAndLoadsTheList()
    {
        await ConnectAsync();
        var first = _connector.LastConnection;
        _connector.Devices = [Strip, Headset];
        var answered = await StartSlowReloadAsync();

        await PassAsync(TimeSpan.FromSeconds(10));
        answered.SetResult();
        await _provider.WhenIdleAsync();

        Assert.Equal(1, _connector.ConnectCount);
        Assert.False(first.IsDisposed);
        Assert.Equal(["Strip", "Headset"], (await _provider.DiscoverAsync(Ct)).Select(device => device.Name));
        Assert.Equal(new ServerStatus(ServerState.Connected, 2), _provider.Status);
        Assert.DoesNotContain(ServerStatus.Connecting, Statuses());
    }

    // A rescan that waits for the slow list answers with it, over the same connection.
    [Fact]
    public async Task Rescan_SearchLongerThanTheLoadTimeout_AnswersWithTheNewList()
    {
        await ConnectAsync();
        var answered = new TaskCompletionSource();
        _connector.DevicesAnswered = answered.Task;
        _connector.DevicesAfterRescan = [Strip, Headset];

        var rescan = _provider.RequestRescanAsync(Ct);
        await Eventually.TrueAsync(() => _connector.DevicesRequests == 2);
        await PassAsync(TimeSpan.FromSeconds(10));
        Assert.False(rescan.IsCompleted);
        answered.SetResult();

        Assert.Equal(RescanOutcome.Rescanned, await rescan);
        Assert.Equal(1, _connector.ConnectCount);
        Assert.Equal(1, _connector.RescanRequests);
        Assert.Equal(2, (await _provider.DiscoverAsync(Ct)).Count);
    }

    // OpenRGB stops while the provider waits: lost at once, without waiting for the timeout, and
    // the next try follows after RetryDelay.
    [Fact]
    public async Task Reload_ConnectionClosesWhileWaiting_DropsItAtOnce()
    {
        await ConnectAsync();
        var first = _connector.LastConnection;
        await StartSlowReloadAsync();

        first.Close();
        await _provider.WhenIdleAsync();

        Assert.True(first.IsDisposed);
        Assert.Empty(await _provider.DiscoverAsync(Ct));
        Assert.Equal(ServerStatus.Connecting, _provider.Status);
        Assert.Equal(1, _connector.ConnectCount);

        _connector.DevicesAnswered = Task.CompletedTask;
        _time.Advance(OpenRgbDeviceProvider.RetryDelay);
        await _provider.WhenIdleAsync();
        Assert.Equal(2, _connector.ConnectCount);
        Assert.Single(await _provider.DiscoverAsync(Ct));
    }

    // Open, but no list at all: OpenRGB may hang, so a new connection at once, which gives it.
    [Fact]
    public async Task Reload_NoListWithinTheReloadTimeout_ConnectsAgainAtOnce()
    {
        await ConnectAsync();
        var first = _connector.LastConnection;
        var timers = _time.Timers;
        await StartSlowReloadAsync();
        _connector.DevicesAnswered = Task.CompletedTask;
        _connector.Devices = [Strip, Headset];

        await PassTheReloadTimeoutAsync(timers);
        await _provider.WhenIdleAsync();

        // Without moving the clock on: no wait for RetryDelay as after a lost connection.
        Assert.Equal(2, _connector.ConnectCount);
        Assert.True(first.IsDisposed);
        Assert.False(_connector.LastConnection.IsDisposed);
        Assert.Equal(2, (await _provider.DiscoverAsync(Ct)).Count);
        Assert.Equal(new ServerStatus(ServerState.Connected, 2), _provider.Status);
    }

    // The new connection after giving up gave the list, so a rescan does not ask it to search
    // again: that would only start another search.
    [Fact]
    public async Task Rescan_NoListWithinTheReloadTimeout_ConnectsAgainWithoutAskingTwice()
    {
        await ConnectAsync();
        var hanging = new TaskCompletionSource();
        _connector.DevicesAnswered = hanging.Task;

        // The fake sends its update with the request, so the rescan waits for it without a
        // RescanTimeout timer: only the reload's two waits count.
        var timers = _time.Timers;
        var rescan = _provider.RequestRescanAsync(Ct);
        await Eventually.TrueAsync(() => _connector.DevicesRequests == 2);
        _connector.DevicesAnswered = Task.CompletedTask;
        await PassTheReloadTimeoutAsync(timers);

        Assert.Equal(RescanOutcome.Rescanned, await rescan);
        Assert.Equal(1, _connector.RescanRequests);
        Assert.Equal(2, _connector.ConnectCount);
        Assert.Single(await _provider.DiscoverAsync(Ct));
    }

    // OpenRGB searched, but its list did not come on either connection: the settings app can say
    // so, rather than that OpenRGB is not reachable.
    [Fact]
    public async Task Rescan_NoListAndConnectingAgainFails_IsListNotLoaded()
    {
        await ConnectAsync();
        _connector.DevicesAnswered = new TaskCompletionSource().Task;

        var timers = _time.Timers;
        var rescan = _provider.RequestRescanAsync(Ct);
        await Eventually.TrueAsync(() => _connector.DevicesRequests == 2);
        _connector.IsRunning = false;
        await PassTheReloadTimeoutAsync(timers);

        Assert.Equal(RescanOutcome.ListNotLoaded, await rescan);
        Assert.Equal(1, _connector.RescanRequests);
        Assert.Equal(2, _connector.ConnectCount);
    }

    // Giving up and connecting anew fails too: then the next try follows after RetryDelay.
    [Fact]
    public async Task Reload_NoListAndConnectingAgainFails_TriesAgainLater()
    {
        await ConnectAsync();
        var timers = _time.Timers;
        await StartSlowReloadAsync();
        _connector.IsRunning = false;

        await PassTheReloadTimeoutAsync(timers);
        await _provider.WhenIdleAsync();

        Assert.Equal(2, _connector.ConnectCount);
        Assert.Equal(ServerState.Unreachable, _provider.Status.State);
        Assert.Empty(await _provider.DiscoverAsync(Ct));
        _connector.IsRunning = true;
        _connector.DevicesAnswered = Task.CompletedTask;
        await RetryAsync(expectedConnects: 3);
        Assert.Single(await _provider.DiscoverAsync(Ct));
    }

    // Exactly ReloadTimeout, in its two waits: LoadTimeout, then the rest.
    private async Task PassTheReloadTimeoutAsync(int timersBefore)
    {
        await _time.AdvanceAfterTimerAsync(timersBefore, OpenRgbDeviceProvider.LoadTimeout);
        await _time.AdvanceAfterTimerAsync(
            timersBefore + 1, OpenRgbDeviceProvider.ReloadTimeout - OpenRgbDeviceProvider.LoadTimeout);
    }

    private async Task ConnectAsync()
    {
        await _provider.DiscoverAsync(Ct);
        await _provider.WhenIdleAsync();
    }

    // No new try a moment before RetryDelay, one when it has passed.
    private async Task RetryAsync(int expectedConnects)
    {
        var tick = TimeSpan.FromTicks(1);
        _time.Advance(OpenRgbDeviceProvider.RetryDelay - tick);
        await _provider.WhenIdleAsync();
        Assert.Equal(expectedConnects - 1, _connector.ConnectCount);

        _time.Advance(tick);
        await _provider.WhenIdleAsync();
        Assert.Equal(expectedConnects, _connector.ConnectCount);
    }

    // OpenRGB tells its list changed but does not answer yet; returns once the provider asked.
    private async Task<TaskCompletionSource> StartSlowReloadAsync()
    {
        var answered = new TaskCompletionSource();
        _connector.DevicesAnswered = answered.Task;
        var requests = _connector.DevicesRequests;
        _connector.LastConnection.RaiseDeviceListChanged();
        await Eventually.TrueAsync(() => _connector.DevicesRequests > requests);
        return answered;
    }

    // In steps, with a moment between them, so the timer the provider starts just after it asked
    // misses at most one step.
    private async Task PassAsync(TimeSpan time)
    {
        for (var passed = TimeSpan.Zero; passed < time; passed += TimeSpan.FromSeconds(1))
        {
            await Task.Delay(10, Ct);
            _time.Advance(TimeSpan.FromSeconds(1));
        }
    }

    private ServerStatus[] Statuses()
    {
        lock (_statuses)
        {
            return [.. _statuses];
        }
    }
}
