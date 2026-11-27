using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BuurmansRgb.UI.ViewModels;

// The other RGB software the engine found running (IEngineApi.GetOtherRgbSoftwareAsync), for the
// status line: one message per program, such as "SignalRGB draait ook en kan je kleuren
// overschrijven.", with "Uitleg" that unfolds what to do (and a link to the guide under it only
// while there is a guide to open, as for "Handleiding" on Instellingen) and "Niet meer melden". This only asks; MainViewModel sends a
// dismissal to the engine, which keeps it.
public sealed partial class OtherRgbSoftwareViewModel : ObservableObject
{
    // The heading "Andere RGB-software" in docs/handleiding.md, as an anchor.
    public const string GuideSection = "andere-rgb-software";

    private readonly AppInfoViewModel _appInfo;

    public OtherRgbSoftwareViewModel(AppInfoViewModel appInfo)
    {
        _appInfo = appInfo;
        DismissCommand = new RelayCommand<string>(Dismiss);
    }

    // Raised with the id of a program the user does not want to hear of again.
    public event EventHandler<string>? DismissRequested;

    // As the engine gives them, in its order; empty while there is none, or no engine.
    [ObservableProperty]
    public partial IReadOnlyList<OtherRgbSoftware> Programs { get; private set; } = [];

    public bool HasGuide => _appInfo.HasGuide;

    public RelayCommand<string> DismissCommand { get; }

    // The sentence for one program; a second OpenRGB has its own, as OpenRGB itself is no stranger.
    public static string TextFor(OtherRgbSoftware software) =>
        software.Id == OtherRgbSoftware.OpenRgbId ? Strings.OtherOpenRgbRuns : UiText.Format(Strings.OtherRgbSoftwareRuns, software.Name);

    // What "Uitleg" unfolds: how to close it and keep it from starting again, in the app itself,
    // as the guide does not come with it (AppInfoViewModel.GuideAddress).
    public static string ExplanationFor(OtherRgbSoftware software) =>
        software.Id == OtherRgbSoftware.OpenRgbId ? Strings.OtherOpenRgbExplanation : UiText.Format(Strings.OtherRgbSoftwareExplanation, software.Name);

    // The name a screen reader gives the buttons of a program.
    public static string NameFor(OtherRgbSoftware software) =>
        software.Id == OtherRgbSoftware.OpenRgbId ? Strings.OtherOpenRgbName : software.Name;

    // Only when it differs, so the status line does not rebuild for the same list.
    public void Show(IReadOnlyList<OtherRgbSoftware> programs)
    {
        if (!programs.SequenceEqual(Programs))
        {
            Programs = programs;
        }
    }

    public void Clear() => Show([]);

    [RelayCommand(CanExecute = nameof(HasGuide))]
    private void OpenGuide() => _appInfo.OpenGuideAt(GuideSection);

    // The message goes at once; the engine confirms with its new list, and a dismissal that did
    // not reach it brings the message back the next time the app reads the list.
    private void Dismiss(string? id)
    {
        if (id is null || !Programs.Any(software => software.Id == id))
        {
            return;
        }

        Programs = [.. Programs.Where(software => software.Id != id)];
        DismissRequested?.Invoke(this, id);
    }
}
