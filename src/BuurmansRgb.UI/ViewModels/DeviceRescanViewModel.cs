using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// "Verversen" on Apparaten: lets the engine search for devices again, for a device that does not
// come back by itself, such as a headset that was switched off and on. While it searches the
// button says "Zoeken…" and stays where it is, so the keyboard focus stays on it; a second click
// then does nothing. It ends when the engine answers, which it does after the new list came, or
// after SearchTimeout, so the button never stays stuck. This only asks; MainViewModel sends it to
// the engine and calls Finish with the answer.
public sealed partial class DeviceRescanViewModel : ObservableObject, IDisposable
{
    private readonly TimeProvider _time;
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private ITimer? _timeoutTimer;

    // Counts the searches, so the answer of an older one (after a timeout, or from a connection
    // that is gone) changes nothing.
    private int _search;

    // When the search that runs ends without an answer; a timeout that was already on its way when
    // a new search started is too early for that one.
    private DateTimeOffset _deadline;

    // time runs the timeout; tests pass a fake one.
    public DeviceRescanViewModel(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    // Raised with the number of the search when the user asks for one; Finish takes it back.
    public event EventHandler<int>? Requested;

    // The engine answers within about 11 s: 5 s for OpenRGB's first update, then connecting anew.
    public static TimeSpan SearchTimeout { get; } = TimeSpan.FromSeconds(15);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ButtonText))]
    public partial bool IsSearching { get; private set; }

    // What the last search left to say: OpenRGB could not be reached (until it is connected
    // again), or it took long.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string? Message { get; private set; }

    public bool HasMessage => Message is not null;

    public string ButtonText => IsSearching ? Strings.RescanSearching : Strings.Rescan;

    // The engine's answer to search number search: false when OpenRGB could not be reached. Also
    // after the timeout, so the message still follows.
    public void Finish(int search, bool openRgbReached)
    {
        if (search != _search)
        {
            return;
        }

        StopTimer();
        IsSearching = false;
        Message = openRgbReached ? null : Strings.RescanOpenRgbUnreachable;
    }

    // OpenRGB is connected again, so the message that it could not be reached no longer holds.
    public void OnOpenRgbConnected()
    {
        if (Message == Strings.RescanOpenRgbUnreachable)
        {
            Message = null;
        }
    }

    // Without the engine nothing is searched, and its answer will not come.
    public void Clear()
    {
        _search++;
        StopTimer();
        IsSearching = false;
        Message = null;
    }

    public void Dispose() => _timeoutTimer?.Dispose();

    [RelayCommand]
    private void Rescan()
    {
        if (IsSearching)
        {
            return;
        }

        var search = ++_search;
        Message = null;
        IsSearching = true;
        _deadline = _time.GetUtcNow() + SearchTimeout;
        _timeoutTimer ??= _time.CreateTimer(_ => OnTimeout(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timeoutTimer.Change(SearchTimeout, Timeout.InfiniteTimeSpan);
        Requested?.Invoke(this, search);
    }

    // The timer runs on the thread pool; the button belongs to the UI thread.
    private void OnTimeout()
    {
        if (_uiContext is null)
        {
            EndSlowSearch();
        }
        else
        {
            _uiContext.Post(_ => EndSlowSearch(), state: null);
        }
    }

    private void EndSlowSearch()
    {
        if (IsSearching && _time.GetUtcNow() >= _deadline)
        {
            IsSearching = false;
            Message = Strings.RescanSlow;
        }
    }

    private void StopTimer() => _timeoutTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
}
