using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using BuurmansRgb.Core;
using BuurmansRgb.Providers.OpenRgb.OpenRgbNet;
using OpenRGB.NET;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

// A small OpenRGB SDK server on a real loopback socket, speaking protocol version 4: enough of
// the protocol for OpenRGB.NET to connect, read devices, set the custom mode, send colors and
// resize zones.
internal sealed class OpenRgbTestServer : IAsyncDisposable
{
    private const uint ProtocolVersion = 4;

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<Socket, bool> _clients = new();
    private readonly Task _acceptLoop;
    private int _controllerDataRequests;

    public OpenRgbTestServer(int port = 0)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = AcceptAsync();
    }

    public int Port { get; }

    public IReadOnlyList<OpenRgbDeviceData> Devices { get; set; } = [TestDevices.Create()];

    // Off: the server never answers the protocol version request, like a hanging server.
    public bool AnswersProtocolVersion { get; set; } = true;

    // Off: device data requests stay unanswered, like a server busy with a rescan.
    public bool AnswersControllerData { get; set; } = true;

    public int ControllerDataRequests => Volatile.Read(ref _controllerDataRequests);

    public ConcurrentQueue<string> ClientNames { get; } = new();

    public ConcurrentQueue<int> CustomModeIndexes { get; } = new();

    public BlockingCollection<(int Index, RgbColor[] Colors)> Updates { get; } = [];

    public BlockingCollection<(int Index, int Zone, int LedCount)> Resizes { get; } = [];

    public int ClientCount => _clients.Count;

    public OpenRgbNetConnector CreateConnector() =>
        new(IPAddress.Loopback.ToString(), Port, "Test", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

    public void NotifyDeviceListUpdated()
    {
        var header = new byte[OpenRgbPackets.HeaderLength];
        OpenRgbPackets.WriteHeader(header, 0, CommandId.DeviceListUpdated, 0);
        SendToClients(header);
    }

    // Sends raw bytes to every client, also bytes that are no valid packet.
    public void SendToClients(byte[] bytes)
    {
        foreach (var client in _clients.Keys)
        {
            client.Send(bytes);
        }
    }

    // A normal close, as when OpenRGB exits: the clients read the end of the stream.
    public void CloseClients()
    {
        foreach (var client in _clients.Keys)
        {
            try
            {
                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
            catch (ObjectDisposedException)
            {
                // That client just left by itself.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_stop.IsCancellationRequested)
        {
            return;
        }

        await _stop.CancelAsync();
        _listener.Stop();
        CloseClients();
        await _acceptLoop;
        _stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            Socket client;
            try
            {
                client = await _listener.AcceptSocketAsync(_stop.Token);
            }
            catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            _clients[client] = true;
            _ = ServeAsync(client);
        }
    }

    private async Task ServeAsync(Socket client)
    {
        try
        {
            await using var stream = new NetworkStream(client, ownsSocket: true);
            var header = new byte[OpenRgbPackets.HeaderLength];
            while (true)
            {
                await stream.ReadExactlyAsync(header, _stop.Token);
                Assert.True(OpenRgbPackets.TryReadHeader(header, out var command, out var length));
                var deviceIndex = (int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
                var data = new byte[length];
                await stream.ReadExactlyAsync(data, _stop.Token);
                if (Answer(command, deviceIndex, data) is { } answer)
                {
                    await stream.WriteAsync(answer, _stop.Token);
                }
            }
        }
        catch (Exception exception) when (exception is EndOfStreamException or IOException or SocketException
            or OperationCanceledException or ObjectDisposedException)
        {
        }
        finally
        {
            _clients.TryRemove(client, out _);
        }
    }

    private byte[]? Answer(CommandId command, int deviceIndex, byte[] data)
    {
        switch (command)
        {
            case CommandId.SetClientName:
                ClientNames.Enqueue(Encoding.ASCII.GetString(data).TrimEnd('\0'));
                return null;
            case CommandId.RequestProtocolVersion:
                return AnswersProtocolVersion ? Packet(command, 0, writer => writer.UInt32(ProtocolVersion)) : null;
            case CommandId.RequestControllerCount:
                return Packet(command, 0, writer => writer.UInt32((uint)Devices.Count));
            case CommandId.RequestControllerData:
                Interlocked.Increment(ref _controllerDataRequests);
                return AnswersControllerData ? Packet(command, deviceIndex, writer => WriteDevice(writer, Devices[deviceIndex])) : null;
            case CommandId.SetCustomMode:
                CustomModeIndexes.Enqueue(deviceIndex);
                return null;
            case CommandId.UpdateLeds:
                Updates.Add((deviceIndex, ReadColors(data)));
                return null;
            case CommandId.ResizeZone:
                // Zone index and new size, each an int32.
                Resizes.Add((deviceIndex, BinaryPrimitives.ReadInt32LittleEndian(data), BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4))));
                return null;
            default:
                return null;
        }
    }

    // UpdateLeds data: total size (uint32), color count (uint16), then R, G, B and a padding byte per LED.
    private static RgbColor[] ReadColors(byte[] data)
    {
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4));
        var colors = new RgbColor[count];
        for (var i = 0; i < count; i++)
        {
            var offset = 6 + i * 4;
            colors[i] = new RgbColor(data[offset], data[offset + 1], data[offset + 2]);
        }

        return colors;
    }

    // The controller data block of protocol version 4, which starts with its own total size.
    private static void WriteDevice(PacketWriter writer, OpenRgbDeviceData device)
    {
        var body = new PacketWriter();
        body.Int32((int)device.Type);
        body.String(device.Name);
        body.String("Vendor");
        body.String("Description");
        body.String("1.0");
        body.String(device.Serial);
        body.String(device.Location);
        body.UInt16((ushort)device.ModeNames.Count);
        body.Int32(0);
        foreach (var mode in device.ModeNames)
        {
            body.String(mode);
            body.Int32(0); // value
            body.UInt32(0); // flags
            for (var i = 0; i < 9; i++)
            {
                body.UInt32(0); // speed min/max, brightness min/max, colors min/max, speed, brightness, direction
            }

            body.UInt32(0); // color mode
            body.UInt16(0); // colors
        }

        body.UInt16((ushort)device.Zones.Count);
        foreach (var zone in device.Zones)
        {
            body.String(zone.Name);
            body.UInt32(zone.Matrix is null ? 1u : 2u); // linear or matrix
            body.UInt32((uint)zone.MinLedCount);
            body.UInt32((uint)zone.MaxLedCount);
            body.UInt32((uint)zone.LedCount);
            WriteMatrix(body, zone.Matrix);
            body.UInt16(0); // no segments
        }

        body.UInt16((ushort)device.LedCount);
        for (var i = 0; i < device.LedCount; i++)
        {
            body.String($"LED {i + 1}");
            body.UInt32(0);
        }

        body.UInt16((ushort)device.LedCount);
        for (var i = 0; i < device.LedCount; i++)
        {
            body.UInt32(0);
        }

        writer.UInt32((uint)(body.Length + sizeof(uint)));
        writer.Bytes(body.ToArray());
    }

    // The size in bytes (uint16), height and width (uint32), then the cells row after row, with
    // 0xFFFFFFFF for a cell without a key. Size 0 means no matrix.
    private static void WriteMatrix(PacketWriter writer, LedMatrix? matrix)
    {
        if (matrix is null)
        {
            writer.UInt16(0);
            return;
        }

        writer.UInt16((ushort)((2 + matrix.Cells.Count) * sizeof(uint)));
        writer.UInt32((uint)matrix.Height);
        writer.UInt32((uint)matrix.Width);
        foreach (var cell in matrix.Cells)
        {
            writer.UInt32(cell == LedMatrix.NoLed ? uint.MaxValue : (uint)cell);
        }
    }

    private static byte[] Packet(CommandId command, int deviceIndex, Action<PacketWriter> writeData)
    {
        var data = new PacketWriter();
        writeData(data);
        var packet = new byte[OpenRgbPackets.HeaderLength + data.Length];
        OpenRgbPackets.WriteHeader(packet, deviceIndex, command, data.Length);
        data.ToArray().CopyTo(packet, OpenRgbPackets.HeaderLength);
        return packet;
    }

    private sealed class PacketWriter
    {
        private readonly MemoryStream _stream = new();

        public int Length => (int)_stream.Length;

        public void UInt16(ushort value) => Write(sizeof(ushort), span => BinaryPrimitives.WriteUInt16LittleEndian(span, value));

        public void UInt32(uint value) => Write(sizeof(uint), span => BinaryPrimitives.WriteUInt32LittleEndian(span, value));

        public void Int32(int value) => Write(sizeof(int), span => BinaryPrimitives.WriteInt32LittleEndian(span, value));

        // Length (uint16) including the closing zero byte, the ASCII text, then the zero byte.
        public void String(string text)
        {
            UInt16((ushort)(text.Length + 1));
            Bytes(Encoding.ASCII.GetBytes(text));
            _stream.WriteByte(0);
        }

        public void Bytes(byte[] bytes) => _stream.Write(bytes);

        public byte[] ToArray() => _stream.ToArray();

        private void Write(int size, SpanAction write)
        {
            Span<byte> buffer = stackalloc byte[size];
            write(buffer);
            _stream.Write(buffer);
        }

        private delegate void SpanAction(Span<byte> span);
    }
}
