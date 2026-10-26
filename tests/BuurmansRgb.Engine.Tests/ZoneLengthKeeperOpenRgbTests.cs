using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Engine.Lighting;
using BuurmansRgb.Providers.OpenRgb.Testing;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.Engine.Tests;

// The zone lengths against the OpenRGB provider, with a server in memory that forgets a length when
// it restarts, as OpenRGB sometimes does.
public sealed class ZoneLengthKeeperOpenRgbTests : IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider _time = new();
    private readonly InMemoryOpenRgb _openRgb;
    private readonly LightingController _lighting;
    private readonly ZoneLengthKeeper _keeper;
    private readonly string _boardId;

    // Device lists the provider reported and the lighting handled, to wait until both are done.
    private int _reported;
    private int _handled;

    public ZoneLengthKeeperOpenRgbTests()
    {
        _openRgb = new InMemoryOpenRgb(_time, RetryDelay);
        _boardId = SetBoard(header: 0);
        _openRgb.Provider.DevicesChanged += (_, _) => Interlocked.Increment(ref _reported);
        _lighting = new LightingController([_openRgb.Provider]);
        _keeper = new ZoneLengthKeeper(_lighting);
        _lighting.DevicesChanged += (_, _) => Interlocked.Increment(ref _handled);
        _keeper.Load(new Dictionary<string, IReadOnlyDictionary<string, int>> { [_boardId] = new Dictionary<string, int> { ["Aura Addressable 1"] = 30 } });
    }

    public void Dispose()
    {
        _keeper.Dispose();
        _lighting.Dispose();
        _openRgb.Dispose();
    }

    [Fact]
    public async Task Connecting_GivesTheHeaderItsLengthOnce()
    {
        await _lighting.DiscoverAsync(Ct);

        await HeaderLengthAsync(30);
        await SettledAsync();
        Assert.Equal([(0, 1, 30)], _openRgb.Resizes);
    }

    // OpenRGB restarts and has forgotten the length: the engine connects again and sets it again,
    // once, without a loop of resizes and reloads.
    [Fact]
    public async Task ServerRestartsWithoutTheLength_GetsItBackOnce()
    {
        await _lighting.DiscoverAsync(Ct);
        await HeaderLengthAsync(30);

        _openRgb.Stop();
        await Eventually.TrueAsync(() => _lighting.Devices.Count == 0);
        SetBoard(header: 0);
        _openRgb.Start();
        _time.Advance(RetryDelay);

        await HeaderLengthAsync(30);
        await SettledAsync();
        Assert.Equal([(0, 1, 30), (0, 1, 30)], _openRgb.Resizes);
    }

    // A rescan of OpenRGB that keeps the length asks for nothing.
    [Fact]
    public async Task RescanWithTheLength_ResizesNothingMore()
    {
        await _lighting.DiscoverAsync(Ct);
        await HeaderLengthAsync(30);
        await SettledAsync();

        _openRgb.Rescan();
        await SettledAsync();

        Assert.Single(_openRgb.Resizes);
    }

    // Tellen against OpenRGB: the header gets its longest length through a reload of the device
    // list, keeps counting through it, and Stoppen gives it its own length back.
    [Fact]
    public async Task CountLeds_ThenStop_GivesTheHeaderItsLengthBack()
    {
        using var counter = new ZoneCounter(_lighting, _keeper);
        await _lighting.DiscoverAsync(Ct);
        await HeaderLengthAsync(30);
        await SettledAsync();
        _lighting.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Static)));

        counter.Start(_boardId, 1, 1);
        await HeaderLengthAsync(120);
        await SettledAsync();
        Assert.True(_lighting.Count.Counts(_boardId, 1));

        counter.Stop(_boardId, 1);
        await HeaderLengthAsync(30);
        await SettledAsync();

        Assert.False(_lighting.Count.IsCounting);
        Assert.Equal([(0, 1, 30), (0, 1, 120), (0, 1, 30)], _openRgb.Resizes);
        Assert.Equal(30, _keeper.Lengths[_boardId]["Aura Addressable 1"]);
    }

    // The settings app closes along with the engine: its count gave the length back before the
    // counter goes, and the engine still waits on the way out until OpenRGB got that length.
    [Fact]
    public async Task Exit_AfterTheOwnerStoppedTheCount_WaitsUntilOpenRgbGotTheLengthBack()
    {
        using var counter = new ZoneCounter(_lighting, _keeper);
        await _lighting.DiscoverAsync(Ct);
        await HeaderLengthAsync(30);
        await SettledAsync();
        _lighting.Apply(LightingStates.Uniform(new DeviceLighting(Orange, 1.0, EffectIds.Static)));
        var app = new object();
        var busy = new TaskCompletionSource();
        _openRgb.HoldDeviceLists(busy.Task);
        counter.Start(_boardId, 1, 1, app);
        await Eventually.TrueAsync(() => _openRgb.Resizes.Count == 2);

        counter.StopFor(app);
        var exit = Task.Run(() => Program.WaitForResizesOnExit(_openRgb.Provider).Dispose(), Ct);

        await Task.Delay(100, Ct);
        Assert.False(exit.IsCompleted);
        busy.SetResult();
        await exit.WaitAsync(TestTimeouts.Short, Ct);
        Assert.Equal([(0, 1, 30), (0, 1, 120), (0, 1, 30)], _openRgb.Resizes);
    }

    // An addressable header on a motherboard, as OpenRGB lists an ASUS board.
    private string SetBoard(int header) =>
        _openRgb.SetMotherboard("ASUS board", ("Aura Mainboard", 2, 2, 2), ("Aura Addressable 1", header, 0, 120));

    private async Task HeaderLengthAsync(int ledCount) =>
        await Eventually.TrueAsync(() => _lighting.Devices.SingleOrDefault()?.Zones[1].LedCount == ledCount);

    // Until the provider has no work left and the lighting (with the keeper) has handled every device
    // list it reported: the discovery itself and each list after it. A resize the keeper asks for
    // queues new work, so this goes on until a round brings nothing new.
    private async Task SettledAsync()
    {
        int reported;
        do
        {
            reported = Volatile.Read(ref _reported);
            await _openRgb.WhenIdleAsync();
            await Eventually.TrueAsync(() => Volatile.Read(ref _handled) == 1 + Volatile.Read(ref _reported));
        }
        while (reported != Volatile.Read(ref _reported));
    }
}
