using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BuurmansRgb.UI.ViewModels;

// The messages of the status line, in the order they matter (StatusLineViewModel).
public enum StatusMessageKind
{
    // The connection with the devices is gone: what the user chooses reaches them only once it is back.
    ConnectionLost,

    // Other RGB software runs and may overwrite the colors, one message per program: before the
    // pause, which is a choice, as this may be why the colors do not do what the user asks.
    OtherRgbSoftware,

    // The effects are paused, or just resumed.
    Paused,

    // The preview stands still because animations in Windows are off.
    PreviewStill,
}

// One message of the status line: a sentence in plain words, and an action when the user can do
// something about it (with the text, name and help text the source gives it now, such as
// "Zoeken…" while a search runs). Empty (no text), it says nothing and is not shown. Whether it
// stands in the line or under "Details" is the status line's to know (Current and Others).
public sealed partial class StatusMessage : ObservableObject
{
    // actionEndsMessage is true for an action that takes its own button away ("Hervatten"): the
    // keyboard focus moves on before that, so it is not lost. dismissCommand is for a message the
    // user can take away for good ("Niet meer melden"), a second button after the action; that
    // always ends the message, so the focus moves on too. explanationLinkCommand is for the link
    // under the explanation (ShowExplanation), such as to the guide.
    public StatusMessage(StatusMessageKind kind, ICommand? actionCommand = null, bool actionEndsMessage = false, ICommand? dismissCommand = null, ICommand? explanationLinkCommand = null)
    {
        Kind = kind;
        ActionCommand = actionCommand;
        ActionEndsMessage = actionEndsMessage;
        DismissCommand = dismissCommand;
        ExplanationLinkCommand = explanationLinkCommand;
    }

    public StatusMessageKind Kind { get; }

    public ICommand? ActionCommand { get; }

    public bool ActionEndsMessage { get; }

    public ICommand? DismissCommand { get; }

    public ICommand? ExplanationLinkCommand { get; }

    // What "Uitleg" unfolds under the message: what the user can do about it, in plain words; null
    // without an explanation, and then there is no "Uitleg".
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExplanation))]
    [NotifyPropertyChangedFor(nameof(HasExplanationLink))]
    public partial string? Explanation { get; private set; }

    // The name of "Uitleg" for a screen reader, such as "Uitleg over SignalRGB".
    [ObservableProperty]
    public partial string? ExplanationAccessibleName { get; private set; }

    // The text of the link under the explanation; null without one.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExplanationLink))]
    public partial string? ExplanationLinkText { get; private set; }

    [ObservableProperty]
    public partial string? ExplanationLinkAccessibleName { get; private set; }

    // "Uitleg" pressed: the explanation shows under the message, in the line or under "Details",
    // until the user presses it again or the message leaves.
    [ObservableProperty]
    public partial bool ShowsExplanation { get; set; }

    public bool HasExplanation => Explanation is not null;

    public bool HasExplanationLink => HasExplanation && ExplanationLinkText is not null && ExplanationLinkCommand is not null;

    // The text on the second button; null without one.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDismiss))]
    public partial string? DismissText { get; private set; }

    [ObservableProperty]
    public partial string? DismissAccessibleName { get; private set; }

    [ObservableProperty]
    public partial string? DismissHelpText { get; private set; }

    public bool HasDismiss => DismissText is not null && DismissCommand is not null;

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

    public bool IsActive => Text.Length > 0;

    public bool HasAction => ActionText is not null && ActionCommand is not null;

    // What the message says now, and what its action says; actionText null while there is none.
    public void Show(string text, string? actionText, string? actionAccessibleName, string? actionHelpText)
    {
        Text = text;
        ActionText = actionText;
        ActionAccessibleName = actionAccessibleName;
        ActionHelpText = actionHelpText;
    }

    // What the second button says; only with a DismissCommand.
    public void ShowDismiss(string text, string accessibleName, string helpText)
    {
        DismissText = text;
        DismissAccessibleName = accessibleName;
        DismissHelpText = helpText;
    }

    // What "Uitleg" unfolds, and the link under it (linkText null without one, or without an
    // ExplanationLinkCommand).
    public void ShowExplanation(string explanation, string accessibleName, string? linkText, string? linkAccessibleName)
    {
        Explanation = explanation;
        ExplanationAccessibleName = accessibleName;
        ExplanationLinkText = linkText;
        ExplanationLinkAccessibleName = linkAccessibleName;
    }

    // The message holds no more and leaves the line; when it comes back, its explanation is closed.
    public void Hide()
    {
        ShowsExplanation = false;
        Explanation = null;
        ExplanationAccessibleName = null;
        ExplanationLinkText = null;
        ExplanationLinkAccessibleName = null;
        Text = string.Empty;
        ActionText = null;
        ActionAccessibleName = null;
        ActionHelpText = null;
        DismissText = null;
        DismissAccessibleName = null;
        DismissHelpText = null;
    }

    // UI Automation names an item of the list under "Details" after this (docs/ontwikkelaars.md,
    // "Schermlezer").
    public override string ToString() => Text;
}
