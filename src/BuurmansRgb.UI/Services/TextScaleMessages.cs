using System.Windows.Threading;

namespace BuurmansRgb.UI.Services;

// The signals that a value of the text size of Windows may have changed, as a Changed on the
// dispatcher once the factor really changed. A signal may come from any thread (SystemTextScale
// gets it on a thread of the pool); the value is read on the dispatcher, a little later, once it has
// nothing more urgent to do. Windows can set the value more than once for one change, so a factor
// that stays the same gives no Changed. SystemTextScale hands it the signals and reads the value
// from the registry; a test hands it its own.
internal sealed class TextScaleMessages : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Func<object?> _read;
    private volatile bool _disposed;

    // read gives the value of TextScaleFactor as Windows stores it, or null without one.
    public TextScaleMessages(Dispatcher dispatcher, Func<object?> read)
    {
        _dispatcher = dispatcher;
        _read = read;
        Factor = FactorOf(read());
    }

    public event EventHandler? Changed;

    public double Factor { get; private set; }

    // The factor of a value of TextScaleFactor: a percentage within the range of Windows, else 1.0.
    internal static double FactorOf(object? value) =>
        value is int percent and >= 100 ? Math.Min(percent, 225) / 100.0 : 1.0;

    // From any thread: the value may have changed.
    public void OnSignaled()
    {
        if (!_disposed)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Background, Refresh);
        }
    }

    // After this, no signal gives a Changed, also not one that is on its way to the dispatcher.
    public void Dispose() => _disposed = true;

    private void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var factor = FactorOf(_read());
        if (factor != Factor)
        {
            Factor = factor;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
