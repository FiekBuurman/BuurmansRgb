using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// "Licht op de achtergrond" on Instellingen: the memory and CPU of the engine right now. The app
// measures them itself (IProcessMeter on the process id the engine gave), so the engine does
// nothing for it, and only while the page shows (IsShown: Instellingen chosen, the window not
// minimized) and the app knows the engine (ProcessId): then a reading every Interval, and none at
// all otherwise; the timer stops as the page goes, the window minimizes or the app closes
// (Dispose). The CPU is the time the engine used between two readings over the time that passed,
// divided by the cores of the pc: a share of the whole pc, as Taakbeheer shows it. The memory is
// the working set, the measure of the budget in docs/bouwplan.md. The first reading only gives the
// memory; the CPU follows with the second.
public sealed partial class EngineUsageViewModel : ObservableObject, IDisposable
{
    private readonly IProcessMeter _meter;
    private readonly TimeProvider _time;
    private readonly int _processorCount;
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
    private readonly ITimer _timer;

    // Guards the meter and the last reading against a tick that runs while the page goes.
    private readonly Lock _gate = new();
    private (ProcessReading Reading, long Timestamp)? _last;

    // Counts the starts and stops, so a tick of a run that stopped shows nothing.
    private int _run;
    private bool _disposed;

    public EngineUsageViewModel(IProcessMeter meter, TimeProvider? time = null, int? processorCount = null)
    {
        _meter = meter;
        _time = time ?? TimeProvider.System;
        _processorCount = Math.Max(1, processorCount ?? Environment.ProcessorCount);
        _timer = _time.CreateTimer(_ => Tick(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    // Often enough to see it move, seldom enough to cost nothing.
    public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(2);

    // Whether the timer runs: only while IsShown and ProcessId are both there.
    public bool IsMeasuring { get; private set; }

    // "Geheugen 26 MB · CPU 0,3 %"; without a reading a line that says it cannot be measured now.
    [ObservableProperty]
    public partial string Text { get; private set; } = Strings.UsageUnknown;

    // The engine of this connection, or null without one.
    [ObservableProperty]
    public partial int? ProcessId { get; set; }

    [ObservableProperty]
    public partial bool IsShown { get; set; }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _run++;
            IsMeasuring = false;
            _timer.Dispose();
            (_meter as IDisposable)?.Dispose();
        }
    }

    partial void OnProcessIdChanged(int? value) => Update();

    partial void OnIsShownChanged(bool value) => Update();

    // Starts, restarts (another engine) or stops the readings. The first one comes at once, on
    // this thread, so the memory shows as the page opens.
    private void Update()
    {
        var measure = IsShown && ProcessId is not null;
        lock (_gate)
        {
            if (_disposed || (!measure && !IsMeasuring))
            {
                return;
            }

            _run++;
            _last = null;
            IsMeasuring = measure;
            var period = measure ? Interval : Timeout.InfiniteTimeSpan;
            _timer.Change(period, period);
            if (!measure)
            {
                // No handle of the engine held open while nothing is read.
                _meter.Release();
            }
        }

        if (measure)
        {
            Tick();
        }
        else
        {
            Text = Strings.UsageUnknown;
        }
    }

    private void Tick()
    {
        string text;
        int run;
        lock (_gate)
        {
            if (!IsMeasuring || ProcessId is not { } processId)
            {
                return;
            }

            run = _run;
            var now = _time.GetTimestamp();
            if (_meter.Read(processId) is { } reading)
            {
                text = Format(reading, now);
                _last = (reading, now);
            }
            else
            {
                _last = null;
                text = Strings.UsageUnknown;
            }
        }

        Show(run, text);
    }

    private string Format(ProcessReading reading, long now)
    {
        var memory = reading.WorkingSetBytes / (1024.0 * 1024.0);
        if (_last is not { } last || _time.GetElapsedTime(last.Timestamp, now) is not { TotalMilliseconds: > 0 } elapsed)
        {
            return UiText.Format(Strings.UsageMemoryOnly, memory);
        }

        var used = Math.Max(0, (reading.ProcessorTime - last.Reading.ProcessorTime).TotalMilliseconds);
        return UiText.Format(Strings.Usage, memory, used / elapsed.TotalMilliseconds / _processorCount * 100);
    }

    // On the UI thread, unless the run stopped or changed meanwhile.
    private void Show(int run, string text)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            Text = text;
            return;
        }

        _uiContext.Post(
            _ =>
            {
                if (run == Volatile.Read(ref _run))
                {
                    Text = text;
                }
            },
            state: null);
    }
}
