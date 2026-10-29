using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace BuurmansRgb.UI.Services;

// The contrast theme of Windows, as WPF reads it (SystemParameters.HighContrast). WPF says it
// changed when Windows turns a contrast theme on or off, and also when another theme takes over,
// as long as it was read since: IsOn reads it each time. Changed comes a little later, once the
// dispatcher has nothing more urgent to do, so WPF has read the new SystemColors by then, and it
// comes once for the burst of messages Windows sends for one change.
internal sealed class SystemHighContrast : IHighContrast, IDisposable
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private bool _pending;

    public SystemHighContrast() => SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;

    public event EventHandler? Changed;

    public bool IsOn => SystemParameters.HighContrast;

    public void Dispose() => SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;

    private void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.HighContrast) || _pending)
        {
            return;
        }

        _pending = true;
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _pending = false;
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }
}
