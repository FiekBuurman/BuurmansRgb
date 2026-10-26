using System.Buffers.Binary;
using System.Text;
using OpenRGB.NET;

namespace BuurmansRgb.Providers.OpenRgb.OpenRgbNet;

// The few packets the server watcher reads and writes itself. Every packet starts with a header:
// "ORGB", device index, command and data length, each number a little-endian uint32.
internal static class OpenRgbPackets
{
    public const int HeaderLength = 16;

    // NET_PACKET_ID_REQUEST_RESCAN_DEVICES, since protocol version 5 (OpenRGB 1.0): the server
    // searches for its devices again. OpenRGB.NET 3.1.1 does not know it.
    public const CommandId RequestRescanDevicesCommand = (CommandId)140;

    private static ReadOnlySpan<byte> Magic => "ORGB"u8;

    // The name is sent as an ASCII string with a closing zero byte.
    public static byte[] SetClientName(string name)
    {
        var nameLength = Encoding.ASCII.GetByteCount(name) + 1;
        var packet = new byte[HeaderLength + nameLength];
        WriteHeader(packet, deviceIndex: 0, CommandId.SetClientName, nameLength);
        Encoding.ASCII.GetBytes(name, packet.AsSpan(HeaderLength));
        return packet;
    }

    // Without data, and the server gives no answer; once its device list changed, it sends
    // DeviceListUpdated to every client.
    public static byte[] RequestRescanDevices()
    {
        var packet = new byte[HeaderLength];
        WriteHeader(packet, deviceIndex: 0, RequestRescanDevicesCommand, dataLength: 0);
        return packet;
    }

    public static void WriteHeader(Span<byte> destination, int deviceIndex, CommandId command, int dataLength)
    {
        Magic.CopyTo(destination);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], (uint)deviceIndex);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], (uint)command);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[12..], (uint)dataLength);
    }

    // False when the bytes are no OpenRGB header, which means the stream is out of step.
    public static bool TryReadHeader(ReadOnlySpan<byte> header, out CommandId command, out uint dataLength)
    {
        if (header.Length < HeaderLength || !header.StartsWith(Magic))
        {
            command = default;
            dataLength = 0;
            return false;
        }

        command = (CommandId)BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
        dataLength = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
        return true;
    }
}
