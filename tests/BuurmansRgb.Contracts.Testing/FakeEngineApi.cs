using System.Reflection;
using BuurmansRgb.Core;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace BuurmansRgb.Contracts.Testing;

// The engine for the tests, in memory: the settings app talks to it directly and the RPC tests put
// it behind a pipe. It answers like the engine, records each call that changes something as text
// in Calls, and lets a test set every answer. One part per subject: this file has the status,
// devices, frames and rescans; the others are named after their subject.
public sealed partial class FakeEngineApi : IEngineApi
{
    public event EventHandler<LightingFrame>? FrameRendered;

    public event EventHandler? DevicesChanged;

    public event EventHandler<SettingsSaveResult>? SettingsSaved;

    public event EventHandler<ServerStatus>? OpenRgbStatusChanged;

    public ServerStatus OpenRgbStatus { get; set; } = new(ServerState.Connected, 2);

    public bool HasOpenRgbStatusSubscribers => OpenRgbStatusChanged is not null;

    public void RaiseOpenRgbStatus(ServerStatus status)
    {
        OpenRgbStatus = status;
        OpenRgbStatusChanged?.Invoke(this, status);
    }

    public PauseReasons Pause { get; set; }

    public bool HasPauseSubscribers => PauseChanged is not null;

    public event EventHandler<PauseReasons>? PauseChanged;

    public void RaisePause(PauseReasons pause)
    {
        Pause = pause;
        PauseChanged?.Invoke(this, pause);
    }

    public bool HasProfilesOrOptionsSubscribers => ProfilesChanged is not null || OptionsChanged is not null;

    // When set, the pause changes to this while GetPauseAsync answers with the older one, as when
    // the event overtakes the answer.
    public PauseReasons? PauseChangeWhileAnswering { get; set; }

    public Task<PauseReasons> GetPauseAsync(CancellationToken ct = default)
    {
        var answer = Pause;
        if (PauseChangeWhileAnswering is { } newer)
        {
            RaisePause(newer);
        }

        return Task.FromResult(answer);
    }

    // Like the engine: ends the manual pause only, and reports the new pause.
    public async Task ResumeEffectsAsync(CancellationToken ct = default)
    {
        await Record("resume");
        RaisePause(Pause & ~PauseReasons.Manual);
    }

    // Only recorded: the engine would close this app and restart; nothing of that happens here.
    public Task RestartAsync(CancellationToken ct = default) => Record("restart");

    // When set, the status changes to this while GetOpenRgbStatusAsync answers with the older one,
    // as when the event overtakes the answer.
    public ServerStatus? StatusChangeWhileAnswering { get; set; }

    public Task<ServerStatus> GetOpenRgbStatusAsync(CancellationToken ct = default)
    {
        var answer = OpenRgbStatus;
        if (StatusChangeWhileAnswering is { } newer)
        {
            RaiseOpenRgbStatus(newer);
        }

        return Task.FromResult(answer);
    }

    // A strip of three LEDs and a fan of two, each with one zone that holds all its LEDs.
    public IReadOnlyList<DeviceInfo> Devices { get; set; } =
    [
        new("strip", "LED strip", DeviceType.LedStrip, 3, [RgbZone.Fixed("LED strip", 3)]) { Source = DeviceSource.Hardware },
        new("fan", "Fan", DeviceType.Fan, 2, [RgbZone.Fixed("Fan", 2)]) { Source = DeviceSource.Hardware },
    ];

    public LightingFrame Frame { get; set; } = LightingFrame.Empty;

    public bool HasFrameSubscribers => FrameRendered is not null;

    public void RaiseFrame(LightingFrame frame) => FrameRendered?.Invoke(this, frame);

    public void RaiseDevicesChanged() => DevicesChanged?.Invoke(this, EventArgs.Empty);

    public void RaiseSaved(bool succeeded) => SettingsSaved?.Invoke(this, new SettingsSaveResult(succeeded));

    public Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default) => Task.FromResult(Devices);

    public IReadOnlyList<string> Effects { get; set; } = [EffectIds.Static, EffectIds.PerLed];

    public Task<IReadOnlyList<string>> GetEffectsAsync(CancellationToken ct = default) => Task.FromResult(Effects);

    public int FrameCalls { get; private set; }

    // When set, GetFrameAsync answers once this task completes, with the frame of that moment.
    public Task? FrameGate { get; set; }

    public async Task<LightingFrame> GetFrameAsync(CancellationToken ct = default)
    {
        FrameCalls++;
        if (FrameGate is { } gate)
        {
            await gate;
        }

        return Frame;
    }

    // What the app said last; null while it said nothing, so the engine sends frames.
    public bool? FramesWanted { get; private set; }

    public List<bool> FramesWantedCalls { get; } = [];

    public Task SetFramesWantedAsync(bool wanted, CancellationToken ct = default)
    {
        FramesWanted = wanted;
        FramesWantedCalls.Add(wanted);
        return Task.CompletedTask;
    }

    // Null for all devices, as in the engine.
    public IReadOnlyList<string>? FrameDevices { get; private set; }

    public List<IReadOnlyList<string>?> FrameDevicesCalls { get; } = [];

    public Task SetFrameDevicesAsync(IReadOnlyList<string>? deviceIds, CancellationToken ct = default)
    {
        FrameDevices = deviceIds;
        FrameDevicesCalls.Add(deviceIds);
        return Task.CompletedTask;
    }

    // Like the engine: the device comes again with the new choice, through DevicesChanged.
    public async Task SetZonesAlikeAsync(string deviceId, bool alike, CancellationToken ct = default)
    {
        await Record($"zones-alike {deviceId} {(alike ? "on" : "off")}");
        Devices = [.. Devices.Select(device => device.Id == deviceId ? device with { ZonesAlike = alike } : device)];
        RaiseDevicesChanged();
    }

    public Task FlashDeviceAsync(string deviceId, CancellationToken ct = default) => Record($"flash {deviceId}");

    public Task FlashZoneAsync(string deviceId, int zoneIndex, CancellationToken ct = default) => Record($"flash {deviceId} zone {zoneIndex}");

    // When set, a rescan answers once this task completes, like an engine that waits for OpenRGB.
    public Task? RescanGate { get; set; }

    // What a rescan answers, such as Unreachable as if OpenRGB could not be reached.
    public RescanOutcome RescanOutcome { get; set; } = RescanOutcome.Rescanned;

    // Off: like an engine before RescanDevicesWithOutcomeAsync, which does not know that method.
    public bool KnowsRescanOutcome { get; set; } = true;

    // Like the engine: the new list goes out through DevicesChanged before the answer.
    public async Task<bool> RescanDevicesAsync(CancellationToken ct = default) =>
        await RescanAsync() != RescanOutcome.Unreachable;

    public Task<RescanOutcome> RescanDevicesWithOutcomeAsync(CancellationToken ct = default) =>
        KnowsRescanOutcome ? RescanAsync() : Task.FromException<RescanOutcome>(MethodNotFound(nameof(RescanDevicesWithOutcomeAsync)));

    // What StreamJsonRpc throws for a method the engine does not have. It makes those itself only,
    // through an internal constructor; when a new StreamJsonRpc changes that, say so plainly.
    private static Exception MethodNotFound(string method)
    {
        var constructor = typeof(RemoteMethodNotFoundException).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic, binder: null,
            [typeof(string), typeof(string), typeof(JsonRpcErrorCode), typeof(object), typeof(object)], modifiers: null)
            ?? throw new InvalidOperationException(
                "The internal constructor of StreamJsonRpc's RemoteMethodNotFoundException has changed; update FakeEngineApi.MethodNotFound.");
        return (Exception)constructor.Invoke(["Method not found", method, JsonRpcErrorCode.MethodNotFound, null, null]);
    }

    private async Task<RescanOutcome> RescanAsync()
    {
        await Record("rescan");
        if (RescanGate is { } gate)
        {
            await gate;
        }

        RaiseDevicesChanged();
        return RescanOutcome;
    }

    public event EventHandler? OtherRgbSoftwareChanged;

    public bool HasOtherRgbSoftwareSubscribers => OtherRgbSoftwareChanged is not null;

    // What the engine found and shows; like the engine, a dismissed one leaves the list.
    public List<OtherRgbSoftware> OtherRgbSoftware { get; set; } = [];

    public void RaiseOtherRgbSoftwareChanged() => OtherRgbSoftwareChanged?.Invoke(this, EventArgs.Empty);

    public Task<IReadOnlyList<OtherRgbSoftware>> GetOtherRgbSoftwareAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<OtherRgbSoftware>>([.. OtherRgbSoftware]);

    public async Task DismissOtherRgbSoftwareAsync(string id, CancellationToken ct = default)
    {
        await Record($"dismiss {id}");
        OtherRgbSoftware.RemoveAll(software => software.Id == id);
        RaiseOtherRgbSoftwareChanged();
    }

    // The process id the engine gives for its memory and CPU on Instellingen.
    public int ProcessId { get; set; } = 4242;

    public Task<int> GetProcessIdAsync(CancellationToken ct = default) => Task.FromResult(ProcessId);
}
