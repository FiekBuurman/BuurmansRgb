using System.Net;
using BuurmansRgb.Providers.OpenRgb.OpenRgbNet;
using OpenRGB.NET;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

public sealed class OpenRgbServerWatcherTests : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly OpenRgbTestServer _server = new();

    [Fact]
    public async Task PacketWithData_IsSkippedAndTheNextPacketIsRead()
    {
        using var watcher = await ConnectAsync();
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.DeviceListChanged += (_, _) => changed.TrySetResult();

        // Longer than the watcher's skip buffer, so it is skipped in more than one read.
        _server.SendToClients(Packet(CommandId.RequestControllerData, new byte[10_000]));
        _server.NotifyDeviceListUpdated();

        await changed.Task.WaitAsync(Timeout, Ct);
        Assert.False(watcher.IsClosed);
    }

    [Fact]
    public async Task HeaderWithBadMagic_ClosesTheWatcher()
    {
        using var watcher = await ConnectAsync();
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.Closed += (_, _) => closed.TrySetResult();
        var header = Packet(CommandId.DeviceListUpdated, []);
        header[0] = (byte)'X';

        _server.SendToClients(header);

        await closed.Task.WaitAsync(Timeout, Ct);
        Assert.True(watcher.IsClosed);
    }

    public ValueTask DisposeAsync() => _server.DisposeAsync();

    private static byte[] Packet(CommandId command, byte[] data)
    {
        var packet = new byte[OpenRgbPackets.HeaderLength + data.Length];
        OpenRgbPackets.WriteHeader(packet, 0, command, data.Length);
        data.CopyTo(packet, OpenRgbPackets.HeaderLength);
        return packet;
    }

    private async Task<OpenRgbServerWatcher> ConnectAsync()
    {
        var watcher = await OpenRgbServerWatcher.ConnectAsync(IPAddress.Loopback.ToString(), _server.Port, "Test watcher", Timeout, Ct);
        await Eventually.TrueAsync(() => _server.ClientNames.Contains("Test watcher"), Timeout);
        return watcher;
    }
}
