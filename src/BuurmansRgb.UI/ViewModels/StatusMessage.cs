using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// The messages of the status line, in the order they matter (StatusLineViewModel).
public enum StatusMessageKind
{
    // The connection with the devices is gone: what the user chooses reaches them only once it is back.
    ConnectionLost,

    // The effects are paused, or just resumed.
    Paused,

    // The preview stands still because animations in Windows are off.
    PreviewStill,
}

// One message of the status line: a sentence in plain words, an action when the user can do
// something about it (with the text, name and help text the source gives it now, such as
// "Zoeken…" while a search runs), and whether a message that matters more pushed it under
// "Details". Empty (no text), it says nothing and is not shown.
public sealed partial class StatusMessage : ObservableObject
{
    // actionEndsMessage is true for an action that takes its own button away ("Hervatten"): the
    // keyboard focus moves on before that, so it is not lost.
    public StatusMessage(StatusMessageKind kind, ICommand? actionCommand = null, bool actionEndsMessage = false)
    {
        Kind = kind;
        ActionCommand = actionCommand;
        ActionEndsMessage = actionEndsMessage;
    }

    public StatusMessageKind Kind { get; }

    public ICommand? ActionCommand { get; }

    public bool ActionEndsMessage { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    public partial string Text { get; private set; } = string.Empty;

    // The text on the button; null while there is nothing to do.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAction))]
    public partial string? ActionText { get; private set; }

    [ObservableProperty]
    public partial string? ActionAccessibleName { get; private set; }

    [ObservableProperty]
    public partial string? ActionHelpText { get; private set; }

    // Set by the status line: another message stands in the line, this one is under "Details".
    [ObservableProperty]
    public partial bool IsDisplaced { get; set; }

    public bool IsActive => Text.Length > 0;

    public bool HasAction => ActionText is not null && ActionCommand is not null;

    // What the message says now; no text means it holds no more and leaves the line.
    public void Show(string? text, string? actionText, string? actionAccessibleName, string? actionHelpText)
    {
        Text = text ?? string.Empty;
        ActionText = text is null ? null : actionText;
        ActionAccessibleName = actionAccessibleName;
        ActionHelpText = actionHelpText;
    }

    // UI Automation names an item of the list under "Details" after this (docs/ontwikkelaars.md,
    // "Schermlezer").
    public override string ToString() => Text;
}
