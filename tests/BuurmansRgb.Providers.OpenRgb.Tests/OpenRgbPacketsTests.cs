using System.Text;
using BuurmansRgb.Providers.OpenRgb.OpenRgbNet;
using OpenRGB.NET;

namespace BuurmansRgb.Providers.OpenRgb.Tests;

public sealed class OpenRgbPacketsTests
{
    [Fact]
    public void WriteHeader_ThenTryReadHeader_GivesCommandAndLength()
    {
        var header = new byte[OpenRgbPackets.HeaderLength];

        OpenRgbPackets.WriteHeader(header, deviceIndex: 3, CommandId.UpdateLeds, dataLength: 42);

        Assert.True(OpenRgbPackets.TryReadHeader(header, out var command, out var dataLength));
        Assert.Equal(CommandId.UpdateLeds, command);
        Assert.Equal(42u, dataLength);
        Assert.Equal("ORGB"u8.ToArray(), header[..4]);
        Assert.Equal([3, 0, 0, 0], header[4..8]);
    }

    [Fact]
    public void TryReadHeader_BadMagic_IsFalse()
    {
        var header = new byte[OpenRgbPackets.HeaderLength];
        OpenRgbPackets.WriteHeader(header, 0, CommandId.DeviceListUpdated, 0);
        header[0] = (byte)'X';

        Assert.False(OpenRgbPackets.TryReadHeader(header, out var command, out var dataLength));
        Assert.Equal(default, command);
        Assert.Equal(0u, dataLength);
    }

    [Fact]
    public void TryReadHeader_TooShort_IsFalse()
    {
        Assert.False(OpenRgbPackets.TryReadHeader("ORGB"u8, out _, out _));
    }

    [Fact]
    public void SetClientName_SendsTheNameWithAClosingZeroByte()
    {
        var packet = OpenRgbPackets.SetClientName("Test");

        Assert.True(OpenRgbPackets.TryReadHeader(packet, out var command, out var dataLength));
        Assert.Equal(CommandId.SetClientName, command);
        Assert.Equal(5u, dataLength);
        Assert.Equal("Test\0", Encoding.ASCII.GetString(packet, OpenRgbPackets.HeaderLength, (int)dataLength));
    }

    [Fact]
    public void RequestRescanDevices_IsAHeaderWithCommand140AndNoData()
    {
        var packet = OpenRgbPackets.RequestRescanDevices();

        Assert.Equal(OpenRgbPackets.HeaderLength, packet.Length);
        Assert.True(OpenRgbPackets.TryReadHeader(packet, out var command, out var dataLength));
        Assert.Equal(140u, (uint)command);
        Assert.Equal(0u, dataLength);
    }
}
