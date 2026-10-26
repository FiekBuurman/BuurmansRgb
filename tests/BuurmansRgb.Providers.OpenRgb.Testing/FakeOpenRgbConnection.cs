using System.Net.Sockets;
using BuurmansRgb.Core;

namespace BuurmansRgb.Providers.OpenRgb.Testing;

// One connection of FakeOpenRgbConnector; it reads and changes the connector's server state.
internal sealed class FakeOpenRgbConnection(FakeOpenRgbConnector connector, bool closedSilently = false) : IOpenRgbConnection
{
    private bool _broken;

    public event EventHandler? Closed;

    public event EventHandler? DeviceListChanged;

    public bool IsClosed { get; private set; } = closedSilently;

    public bool IsDisposed { get; private set; }

    public bool FailSends { get; set; }

    // As if another call still waits for the server: colors are skipped.
    public bool IsBusy { get; set; }

    public int ProtocolVersion => 4;

    public Task<IReadOnlyList<OpenRgbDeviceData>> GetDevicesAsync(CancellationToken ct)
    {
        ThrowIfGone();
        if (connector.CloseWhileLoadingDevices)
        {
            Close();
        }

        var answer = connector.GetDevicesException is { } exception
            ? Task.FromException<IReadOnlyList<OpenRgbDeviceData>>(exception)
            : Task.FromResult(connector.Devices);
        return connector.DevicesAnswered.IsCompleted
            ? answer
            : connector.DevicesAnswered.ContinueWith(_ => answer, ct, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
    }

    public void SetCustomMode(int deviceIndex)
    {
        ThrowIfGone();
        connector.CustomModeIndexes.Add(deviceIndex);
    }

    // Like OpenRGB: no answer to the request itself; the device list update follows.
    public Task RequestRescanAsync(CancellationToken ct)
    {
        ThrowIfGone();
        if (FailSends)
        {
            throw new SocketException((int)SocketError.ConnectionReset);
        }

        connector.RescanRequests++;
        if (connector.AnswersRescan)
        {
            connector.Devices = connector.DevicesAfterRescan ?? connector.Devices;
            RaiseDeviceListChanged();
        }

        return Task.CompletedTask;
    }

    // Like OpenRGB: the device gets its new length, without a device list update.
    public void ResizeZone(int deviceIndex, int zoneIndex, int ledCount)
    {
        ThrowIfGone();
        if (FailSends)
        {
            throw new SocketException((int)SocketError.ConnectionReset);
        }

        connector.Resizes.Enqueue((deviceIndex, zoneIndex, ledCount));
        connector.Devices =
        [
            .. connector.Devices.Select(device => device.Index != deviceIndex ? device : TestDevices.Resize(device, zoneIndex, ledCount)),
        ];
    }

    public bool TryUpdateLeds(int deviceIndex, ReadOnlySpan<RgbColor> colors)
    {
        ThrowIfGone();
        if (FailSends)
        {
            throw new SocketException((int)SocketError.ConnectionReset);
        }

        if (IsBusy)
        {
            return false;
        }

        connector.Updates.Enqueue((deviceIndex, colors.ToArray()));
        return true;
    }

    public void Close()
    {
        if (!IsClosed)
        {
            IsClosed = true;
            _broken = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RaiseDeviceListChanged() => DeviceListChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() => IsDisposed = true;

    private void ThrowIfGone()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (_broken)
        {
            throw new SocketException((int)SocketError.ConnectionAborted);
        }
    }
}
