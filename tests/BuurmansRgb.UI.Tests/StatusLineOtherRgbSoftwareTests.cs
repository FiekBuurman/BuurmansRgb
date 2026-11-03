using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace BuurmansRgb.UI.Tests;

// Other RGB software in the status line: one message per program, where it stands among the
// others, "Uitleg" to the guide and "Niet meer melden"; with fake sources, no engine.
public sealed class StatusLineOtherRgbSoftwareTests : IDisposable
{
    private const string Guide = "https://example.org/handleiding";

    private static readonly OtherRgbSoftware SignalRgb = new("signalrgb", "SignalRGB");
    private static readonly OtherRgbSoftware Icue = new("icue", "Corsair iCUE");
    private static readonly OtherRgbSoftware SecondOpenRgb = new(OtherRgbSoftware.OpenRgbId, "OpenRGB");

    private readonly FakeTimeProvider _time = new();
    private readonly FakeAnimationEffects _animations = new();
    private readonly OpenRgbStatusViewModel _openRgb = new();
    private readonly List<string> _opened = [];
    private readonly List<string> _dismissed = [];
    private readonly EffectsPauseViewModel _pause;
    private readonly PreviewMotionViewModel _motion;
    private readonly DeviceRescanViewModel _rescan;
    private readonly OtherRgbSoftwareViewModel _otherRgbSoftware;
    private readonly StatusLineViewModel _line;

    public StatusLineOtherRgbSoftwareTests()
    {
        _pause = new EffectsPauseViewModel(_time);
        _motion = new PreviewMotionViewModel(_animations);
        _rescan = new DeviceRescanViewModel(_time);
        _otherRgbSoftware = new OtherRgbSoftwareViewModel(new AppInfoViewModel(_opened.Add, "engine.log", "1.6.0", Guide));
        _otherRgbSoftware.DismissRequested += (_, id) => _dismissed.Add(id);
        _line = new StatusLineViewModel(_openRgb, _pause, _motion, _rescan, _otherRgbSoftware);
    }

    public void Dispose()
    {
        _line.Dispose();
        _pause.Dispose();
        _motion.Dispose();
        _rescan.Dispose();
    }

    [Fact]
    public void OneProgram_SaysWhichWithUitlegAndNietMeerMelden()
    {
        _otherRgbSoftware.Show([SignalRgb]);

        var current = Assert.IsType<StatusMessage>(_line.Current);
        Assert.Equal(StatusMessageKind.OtherRgbSoftware, current.Kind);
        Assert.Equal("SignalRGB draait ook en kan je kleuren overschrijven.", current.Text);
        Assert.Equal(Strings.OtherRgbSoftwareGuide, current.ActionText);
        Assert.Equal("Uitleg over SignalRGB in de handleiding", current.ActionAccessibleName);
        Assert.Equal(Strings.OtherRgbSoftwareGuideToolTip, current.ActionHelpText);
        Assert.True(current.HasAction);
        Assert.Equal(Strings.OtherRgbSoftwareDismiss, current.DismissText);
        Assert.Equal("SignalRGB niet meer melden", current.DismissAccessibleName);
        Assert.Equal(Strings.OtherRgbSoftwareDismissToolTip, current.DismissHelpText);
        Assert.True(current.HasDismiss);
        Assert.False(_line.HasDetails);
    }

    // "Uitleg" opens the guide at "Andere RGB-software".
    [Fact]
    public void Uitleg_OpensTheGuideAtAndereRgbSoftware()
    {
        _otherRgbSoftware.Show([SignalRgb]);

        _line.Current!.ActionCommand!.Execute(null);

        Assert.Equal([Guide + "#andere-rgb-software"], _opened);
    }

    // As "Handleiding" on Instellingen: no guide to open, no button; "Niet meer melden" stays.
    [Fact]
    public void WithoutAGuide_NoUitleg()
    {
        var otherRgbSoftware = new OtherRgbSoftwareViewModel(new AppInfoViewModel(_opened.Add, "engine.log", "1.6.0"));
        using var line = new StatusLineViewModel(_openRgb, _pause, _motion, _rescan, otherRgbSoftware);

        otherRgbSoftware.Show([SignalRgb]);

        Assert.False(line.Current?.HasAction);
        Assert.True(line.Current?.HasDismiss);
    }

    [Fact]
    public void ASecondOpenRgb_HasItsOwnSentence()
    {
        _otherRgbSoftware.Show([SecondOpenRgb]);

        Assert.Equal(Strings.OtherOpenRgbRuns, _line.Current?.Text);
        Assert.Equal("de tweede OpenRGB niet meer melden", _line.Current?.DismissAccessibleName);
    }

    // After the connection, before the pause and the preview: the other software may be why the
    // colors do not do what the user asks, a pause is a choice. Each program has a message of
    // its own, in the order of the engine.
    [Fact]
    public void WithTheOthers_AfterTheConnectionAndBeforeThePause()
    {
        _animations.Turn(on: false);
        _pause.Show(PauseReasons.Manual);
        _openRgb.DevicesExpected = true;
        _openRgb.Show(new ServerStatus(ServerState.Unreachable));

        _otherRgbSoftware.Show([SignalRgb, Icue]);

        Assert.Equal(StatusMessageKind.ConnectionLost, _line.Current?.Kind);
        Assert.Equal(
            [StatusMessageKind.OtherRgbSoftware, StatusMessageKind.OtherRgbSoftware, StatusMessageKind.Paused, StatusMessageKind.PreviewStill],
            _line.Others.Select(message => message.Kind));
        Assert.Equal("Corsair iCUE draait ook en kan je kleuren overschrijven.", _line.Others[1].Text);
        Assert.Equal("Details: nog 4 meldingen", _line.DetailsAccessibleName);
    }

    [Fact]
    public void BeforeThePause_TakesTheLine()
    {
        _pause.Show(PauseReasons.Manual);

        _otherRgbSoftware.Show([SignalRgb]);

        Assert.Equal(StatusMessageKind.OtherRgbSoftware, _line.Current?.Kind);
        Assert.Equal(StatusMessageKind.Paused, Assert.Single(_line.Others).Kind);
    }

    // "Niet meer melden" takes the message away at once and asks the engine to keep it so; the
    // pause gets the line back.
    [Fact]
    public void NietMeerMelden_TakesItAwayAndTellsTheEngine()
    {
        _pause.Show(PauseReasons.Manual);
        _otherRgbSoftware.Show([SignalRgb, Icue]);
        var signalRgb = _line.Current!;

        signalRgb.DismissCommand!.Execute(null);

        Assert.Equal(["signalrgb"], _dismissed);
        Assert.Equal("Corsair iCUE draait ook en kan je kleuren overschrijven.", _line.Current?.Text);
        Assert.Equal(StatusMessageKind.Paused, Assert.Single(_line.Others).Kind);
        Assert.False(signalRgb.IsActive);
        Assert.False(signalRgb.HasDismiss);
    }

    // From under "Details" too; the message in the line stays.
    [Fact]
    public void NietMeerMeldenUnderDetails_LeavesTheLine()
    {
        _otherRgbSoftware.Show([SignalRgb, Icue]);
        var current = _line.Current;

        _line.Others[0].DismissCommand!.Execute(null);

        Assert.Equal(["icue"], _dismissed);
        Assert.Same(current, _line.Current);
        Assert.Empty(_line.Others);
        Assert.False(_line.HasDetails);
    }

    // The program stopped: its message goes, and comes back as the same item when it runs again.
    [Fact]
    public void TheProgramStopsAndComesBack_TheMessageFollows()
    {
        _otherRgbSoftware.Show([SignalRgb]);
        var message = _line.Current;

        _otherRgbSoftware.Clear();
        Assert.False(_line.IsShown);

        _otherRgbSoftware.Show([SignalRgb]);
        Assert.Same(message, _line.Current);
        Assert.True(message!.IsActive);
    }

    // A message that does not change does not rebuild the list under "Details".
    [Fact]
    public void TheSameListAgain_ChangesNothing()
    {
        _otherRgbSoftware.Show([SignalRgb, Icue]);
        var changes = 0;
        _line.Others.CollectionChanged += (_, _) => changes++;

        _otherRgbSoftware.Show([new OtherRgbSoftware("signalrgb", "SignalRGB"), new OtherRgbSoftware("icue", "Corsair iCUE")]);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void Dispose_StopsFollowingTheSoftware()
    {
        _line.Dispose();

        _otherRgbSoftware.Show([SignalRgb]);

        Assert.Null(_line.Current);
    }
}
