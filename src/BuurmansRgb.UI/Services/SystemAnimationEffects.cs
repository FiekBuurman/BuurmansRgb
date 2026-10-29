using System.Windows;
using System.Windows.Threading;

namespace BuurmansRgb.UI.Services;

// "Animatie-effecten" of Windows, as WPF reads it (SystemParameters.ClientAreaAnimation, from
// SPI_GETCLIENTAREAANIMATION). WPF says it changed when Windows sends SPI_SETCLIENTAREAANIMATION, as
// long as it was read since: IsOn reads it each time. SystemParameterMessages turns that into one
// Changed on the UI thread. It only reads the setting; nothing here changes it.
internal sealed class SystemAnimationEffects : IAnimationEffects, IDisposable
{
    private readonly SystemParameterMessages _messages = new(Dispatcher.CurrentDispatcher, nameof(SystemParameters.ClientAreaAnimation));

    public SystemAnimationEffects()
    {
        _messages.Changed += OnChanged;
        SystemParameters.StaticPropertyChanged += _messages.OnSystemParameterChanged;
    }

    public event EventHandler? Changed;

    public bool IsOn => SystemParameters.ClientAreaAnimation;

    public void Dispose()
    {
        SystemParameters.StaticPropertyChanged -= _messages.OnSystemParameterChanged;
        _messages.Changed -= OnChanged;
    }

    private void OnChanged(object? sender, EventArgs e) => Changed?.Invoke(this, e);
}
