using BuurmansRgb.Contracts;

namespace BuurmansRgb.Engine.Lighting;

// The options that are no part of the lighting: frames per second, what a pause shows, the length
// of a scene fade and the devices whose zones show one image.
internal sealed partial class LightingController
{
    public const int DefaultFramesPerSecond = 30;
    public const int MaxFramesPerSecond = 60;

    private readonly HashSet<string> _zonesAlike = new(StringComparer.Ordinal);

    private int _framesPerSecond = DefaultFramesPerSecond;
    private PausedLightsMode _pausedLights = PausedLightsMode.Keep;
    private TimeSpan _sceneFade = Contracts.SceneFade.Default;

    // Raised when FramesPerSecond got another value; read FramesPerSecond for the newest one.
    public event EventHandler? FramesPerSecondChanged;

    // Kept between 1 and MaxFramesPerSecond. Effects are time-based, so a lower rate only makes
    // the animation less smooth, not slower.
    public int FramesPerSecond
    {
        get
        {
            lock (_gate)
            {
                return _framesPerSecond;
            }
        }
        set => SetOption(
            () =>
            {
                var framesPerSecond = Math.Clamp(value, 1, MaxFramesPerSecond);
                var changed = framesPerSecond != _framesPerSecond;
                _framesPerSecond = framesPerSecond;
                _animation.Period = FramePeriodLocked;
                return changed;
            },
            () => FramesPerSecondChanged?.Invoke(this, EventArgs.Empty));
    }

    // Raised when PausedLights got another value; read PausedLights for the newest one.
    public event EventHandler? PausedLightsChanged;

    // What the devices show while the pause has a reason besides the manual one: the last frame
    // (Keep), that frame at DimmedBrightness (Dim) or black (Off). Only what goes to the devices
    // changes; the buffers, and so the preview, keep the real colors, and when the last of those
    // reasons ends the devices get them again. Another value during such a pause goes out at once;
    // it holds and is reported even when a device fails then.
    public PausedLightsMode PausedLights
    {
        get
        {
            lock (_gate)
            {
                return _pausedLights;
            }
        }
        // Not through SetOption: only this option is reported when a device fails.
        set
        {
            var changed = false;
            try
            {
                lock (_gate)
                {
                    var oldScale = SendScaleLocked;
                    changed = _pausedLights != value;
                    _pausedLights = value;
                    if (SendScaleLocked != oldScale)
                    {
                        SendAllLocked();
                    }
                }
            }
            finally
            {
                if (changed)
                {
                    PausedLightsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    // Raised when SceneFade got another value; read SceneFade for the newest one.
    public event EventHandler? SceneFadeChanged;

    // How long a change with fade (a scene, "Alles aan" or "Alles uit") takes to go over from the
    // colors the devices show to the new ones, kept between zero and SceneFade.Max; zero is at once.
    // A fade that runs keeps its own length.
    public TimeSpan SceneFade
    {
        get
        {
            lock (_gate)
            {
                return _sceneFade;
            }
        }
        set => SetOption(
            () =>
            {
                var fade = Contracts.SceneFade.Clamp(value);
                var changed = fade != _sceneFade;
                _sceneFade = fade;
                return changed;
            },
            () => SceneFadeChanged?.Invoke(this, EventArgs.Empty));
    }

    // Raised when ZonesAlike got other devices; read ZonesAlike for the newest ones.
    public event EventHandler? ZonesAlikeChanged;

    // The ids of the devices whose zones show one image: the effect renders over the longest zone
    // and every other zone gets it stretched (DeviceOutput.Show). It also holds devices that are
    // not there now, so they have it again when they come back. Not with the per-LED effect,
    // where every LED has a color of its own. It is no part of the lighting: a scene leaves it as
    // it is, and LightingChanged is not raised. The devices show another set at once.
    public IReadOnlySet<string> ZonesAlike
    {
        get
        {
            lock (_gate)
            {
                return new HashSet<string>(_zonesAlike, StringComparer.Ordinal);
            }
        }
        set => SetOption(
            () =>
            {
                if (_zonesAlike.SetEquals(value))
                {
                    return false;
                }

                _zonesAlike.Clear();
                _zonesAlike.UnionWith(value);
                return true;
            },
            () => ZonesAlikeChanged?.Invoke(this, EventArgs.Empty),
            render: true);
    }

    // One device, which must be there, such as from the switch in the settings app.
    public void SetZonesAlike(string deviceId, bool alike) => SetOption(
        () =>
        {
            KnownDeviceLocked(deviceId);
            return alike ? _zonesAlike.Add(deviceId) : _zonesAlike.Remove(deviceId);
        },
        () => ZonesAlikeChanged?.Invoke(this, EventArgs.Empty),
        render: true);

    private TimeSpan FramePeriodLocked => TimeSpan.FromSeconds(1.0 / _framesPerSecond);

    // A change of an option: changeLocked runs under the lock and returns whether the option got
    // another value; only then, with render, the devices show it. Outside the lock Rendered goes
    // out, then raiseChanged. When a device fails while it renders, the new value holds but
    // raiseChanged is not called (see docs/ideas.md).
    private void SetOption(Func<bool> changeLocked, Action raiseChanged, bool render = false)
    {
        bool changed;
        var rendered = false;
        lock (_gate)
        {
            changed = changeLocked();
            if (changed && render)
            {
                rendered = RenderLocked();
            }
        }

        Publish(rendered);
        if (changed)
        {
            raiseChanged();
        }
    }
}
