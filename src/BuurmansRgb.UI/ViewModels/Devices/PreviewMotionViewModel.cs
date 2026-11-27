using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// Whether the preview moves. With "Animatie-effecten" of Windows off, it stands still: the app
// shows one image of what the devices do instead of every frame (MainViewModel), until the user
// lets it run for as long as the app is open ("Laten lopen") or still it again ("Stilzetten").
// The devices themselves always do what the user chose. It follows the setting while the app is
// open. Use it from the UI thread, where IAnimationEffects says its Changed.
public sealed partial class PreviewMotionViewModel : ObservableObject, IDisposable
{
    private readonly IAnimationEffects _animations;
    private bool _wasStill;

    public PreviewMotionViewModel(IAnimationEffects animations)
    {
        _animations = animations;
        _wasStill = IsStill;
        _animations.Changed += OnAnimationsChanged;
    }

    // Raised when IsStill changes, after the properties said so.
    public event EventHandler? StillChanged;

    // "Laten lopen" for the rest of this session, also when Windows turns animations on and off
    // again meanwhile.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    public partial bool RunsAnyway { get; private set; }

    // The bar with the button shows only while Windows has animations off.
    public bool IsShown => !_animations.IsOn;

    public bool IsStill => IsShown && !RunsAnyway;

    public string Text => IsStill ? Strings.PreviewStill : Strings.PreviewRunsAnyway;

    public string ButtonText => IsStill ? Strings.PreviewRun : Strings.PreviewStop;

    public string ButtonAccessibleName => IsStill ? Strings.PreviewRunAccessibleName : Strings.PreviewStopAccessibleName;

    public string ButtonHelpText => IsStill ? Strings.PreviewRunHelp : Strings.PreviewStopHelp;

    public void Dispose() => _animations.Changed -= OnAnimationsChanged;

    [RelayCommand(CanExecute = nameof(IsShown))]
    private void Toggle() => RunsAnyway = !RunsAnyway;

    partial void OnRunsAnywayChanged(bool value) => Refresh();

    private void OnAnimationsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsShown));
        ToggleCommand.NotifyCanExecuteChanged();
        Refresh();
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(IsStill));
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(ButtonText));
        OnPropertyChanged(nameof(ButtonAccessibleName));
        OnPropertyChanged(nameof(ButtonHelpText));
        if (IsStill != _wasStill)
        {
            _wasStill = IsStill;
            StillChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
