using System.Globalization;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The length of a resizable zone as the user sets it: with − and +, a slider, or by typing it and
// choosing Apply. LedCount is the length the preview shows at once. The engine only gets it
// SendDelay after the last step, when a slider drag ends, or right away with Apply: OpenRGB
// reloads its devices on every resize, so ten clicks on + or a drag send one resize. Made on the
// UI thread; the delay comes back to it.
public sealed partial class ZoneLengthViewModel : ObservableObject, IDisposable
{
    private readonly RgbZone _zone;
    private readonly IPreviewActions? _actions;
    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;

    // Made on the first change, so a zone nobody touches holds no timer.
    private ITimer? _sendTimer;

    // The length last handed to the actions, or the zone's own length before that.
    private int _sent;
    private bool _dragging;
    private bool _disposed;

    // zoneAccessibleName names the zone for a screen reader, such as "Motherboard – ARGB header".
    public ZoneLengthViewModel(string deviceId, int zoneIndex, RgbZone zone, string zoneAccessibleName, IPreviewActions? actions, TimeProvider? time = null)
    {
        DeviceId = deviceId;
        ZoneIndex = zoneIndex;
        _zone = zone;
        _actions = actions;
        _time = time ?? TimeProvider.System;
        _sent = zone.LedCount;
        LedCount = zone.LedCount;
        LedCountText = FormatCount(zone.LedCount);
        Label = UiText.Format(Strings.ZoneLedCountLabel, zone.MinLedCount, zone.MaxLedCount);
        SliderAccessibleName = UiText.Format(Strings.ZoneLedCountAccessibleName, zoneAccessibleName);
        TextAccessibleName = UiText.Format(Strings.ZoneLedCountTextAccessibleName, zoneAccessibleName);
        DecreaseAccessibleName = UiText.Format(Strings.DecreaseLedCountAccessibleName, zoneAccessibleName);
        IncreaseAccessibleName = UiText.Format(Strings.IncreaseLedCountAccessibleName, zoneAccessibleName);
        ApplyAccessibleName = UiText.Format(Strings.ApplyLedCountAccessibleName, zoneAccessibleName);
    }

    // Long enough for a few clicks or key presses in a row, short enough to feel direct.
    public static TimeSpan SendDelay { get; } = TimeSpan.FromMilliseconds(600);

    public string DeviceId { get; }

    // The index of the zone in the zones of its device.
    public int ZoneIndex { get; }

    // Tells the controls of this zone from those of every other zone, also of a device with the same
    // name, so the view can give them the focus back after the preview is made again (FocusKeeper).
    // The view adds the role of each control.
    public string FocusKey => $"zone-length/{DeviceId}/{ZoneIndex}";

    public int Minimum => _zone.MinLedCount;

    public int Maximum => _zone.MaxLedCount;

    public string Label { get; }

    public string SliderAccessibleName { get; }

    public string TextAccessibleName { get; }

    public string DecreaseAccessibleName { get; }

    public string IncreaseAccessibleName { get; }

    public string ApplyAccessibleName { get; }

    // A length the engine has not been asked for yet, because the delay still runs or a drag goes on.
    public bool HasUnsentChange => LedCount != _sent;

    // The length the preview shows, within Minimum and Maximum. The slider sets it; a new value is
    // sent after SendDelay, or when the drag ends.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial int LedCount { get; set; }

    // The length the user types; Apply shows and sends it. Follows LedCount.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand))]
    public partial string LedCountText { get; set; }

    // The slider's thumb is held: the length goes out when it is let go.
    public void BeginDrag()
    {
        _dragging = true;
        StopTimer();
    }

    public void EndDrag()
    {
        _dragging = false;
        SendNow();
    }

    // Goes on with a length that an earlier view model of this zone did not send yet, for example
    // because the engine reported the devices again halfway: the preview keeps it and it still goes
    // out after the delay. A length the zone cannot hold (any more) is dropped.
    public void ContinueWith(int ledCount)
    {
        if (_zone.CanHold(ledCount))
        {
            LedCount = ledCount;
        }
    }

    // Hands the length to the actions, unless the engine was asked for it already or a drag goes on.
    public void SendNow()
    {
        StopTimer();
        if (_disposed || _dragging || !HasUnsentChange)
        {
            return;
        }

        _sent = LedCount;
        ApplyCommand.NotifyCanExecuteChanged();
        _actions?.ResizeZone(new ZoneResize(DeviceId, ZoneIndex, LedCount));
    }

    public void Dispose()
    {
        _disposed = true;
        _sendTimer?.Dispose();
    }

    partial void OnLedCountChanging(int value) => _zone.ThrowIfCannotHold(value);

    partial void OnLedCountChanged(int value)
    {
        LedCountText = FormatCount(value);
        if (_dragging || _disposed)
        {
            return;
        }

        // Back at the length that was sent, there is nothing to send.
        if (HasUnsentChange)
        {
            _sendTimer ??= _time.CreateTimer(_ => OnSendTimer(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _sendTimer.Change(SendDelay, Timeout.InfiniteTimeSpan);
        }
        else
        {
            StopTimer();
        }
    }

    // − and + stay enabled at the ends of the range and then change nothing: a button that turns
    // off while it has the keyboard focus drops the focus.
    [RelayCommand]
    private void Decrease() => LedCount = Math.Max(LedCount - 1, Minimum);

    [RelayCommand]
    private void Increase() => LedCount = Math.Min(LedCount + 1, Maximum);

    // Only a length the zone can hold, and not the one the engine was asked for last.
    [RelayCommand(CanExecute = nameof(CanApply))]
    private void Apply()
    {
        if (TryReadText(out var ledCount))
        {
            LedCount = ledCount;
            SendNow();
        }
    }

    private bool CanApply() => TryReadText(out var ledCount) && ledCount != _sent;

    private bool TryReadText(out int ledCount) =>
        int.TryParse(LedCountText, NumberStyles.Integer, CultureInfo.CurrentCulture, out ledCount) && _zone.CanHold(ledCount);

    // The timer runs on the thread pool; the length is read and sent on the UI thread.
    private void OnSendTimer()
    {
        if (_uiContext is null)
        {
            SendNow();
        }
        else
        {
            _uiContext.Post(_ => SendNow(), state: null);
        }
    }

    private void StopTimer() => _sendTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    private static string FormatCount(int ledCount) => ledCount.ToString(CultureInfo.CurrentCulture);
}
