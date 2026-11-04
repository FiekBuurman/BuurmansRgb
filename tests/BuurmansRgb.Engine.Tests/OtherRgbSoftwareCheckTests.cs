using BuurmansRgb.Contracts;
using BuurmansRgb.Engine.Programs;

namespace BuurmansRgb.Engine.Tests;

public sealed class OtherRgbSoftwareCheckTests : IDisposable
{
    private static readonly OtherRgbSoftware SignalRgb = new("signalrgb", "SignalRGB");
    private static readonly OtherRgbSoftware Icue = new("icue", "Corsair iCUE");

    private readonly FakeProcessSnapshot _processes = new();
    private readonly FakeResumeEvents _resume = new();
    private readonly OtherRgbSoftwareCheck _check;
    private int _changes;
    private int _dismissedChanges;

    public OtherRgbSoftwareCheckTests()
    {
        _check = new OtherRgbSoftwareCheck(_processes, _resume);
        _check.Changed += (_, _) => _changes++;
        _check.DismissedChanged += (_, _) => _dismissedChanges++;
    }

    public void Dispose() => _check.Dispose();

    // Nothing is read until asked: no timer, no polling.
    [Fact]
    public void New_DoesNotReadTheProcessList()
    {
        Assert.Equal(0, _processes.TakeCount);
        Assert.Empty(_check.Shown);
    }

    [Fact]
    public void Check_SoftwareRuns_ShowsItAndTellsSo()
    {
        _processes.Run("SignalRgb");

        _check.Check();

        Assert.Equal([SignalRgb], _check.Shown);
        Assert.Equal(1, _changes);
        Assert.Equal(1, _processes.TakeCount);
    }

    [Fact]
    public void Check_TheSameAgain_DoesNotTellAgain()
    {
        _processes.Run("SignalRgb");
        _check.Check();

        _check.Check();

        Assert.Equal(1, _changes);
    }

    [Fact]
    public void Check_TheSoftwareStopped_ShowsItNoMore()
    {
        _processes.Run("SignalRgb");
        _check.Check();

        _processes.Processes.Clear();
        _check.Check();

        Assert.Empty(_check.Shown);
        Assert.Equal(2, _changes);
    }

    // A list that does not read keeps the message as it was, so it does not come and go.
    [Fact]
    public void Check_TheListDoesNotRead_KeepsWhatWasFound()
    {
        _processes.Run("SignalRgb");
        _check.Check();

        _processes.TakeException = new InvalidOperationException("No list.");
        _check.Check();

        Assert.Equal([SignalRgb], _check.Shown);
        Assert.Equal(1, _changes);
    }

    // After waking up, once the lights are back: software may have started with Windows meanwhile.
    [Fact]
    public void Restored_LooksAgain()
    {
        _processes.Run("iCUE");

        _resume.RaiseRestored();

        Assert.Equal([Icue], _check.Shown);
        Assert.Equal(1, _processes.TakeCount);
    }

    [Fact]
    public void Dispose_StopsListeningToTheWakeUp()
    {
        _check.Dispose();

        Assert.False(_resume.HasListeners);
    }

    [Fact]
    public void Dismiss_HidesItAndTellsTheHost()
    {
        _processes.Run("SignalRgb").Run("iCUE");
        _check.Check();

        _check.Dismiss("signalrgb");

        Assert.Equal([Icue], _check.Shown);
        Assert.Equal([SignalRgb, Icue], _check.Found);
        Assert.Equal(["signalrgb"], _check.Dismissed);
        Assert.Equal(1, _dismissedChanges);
        Assert.Equal(2, _changes);
    }

    // For good: also when the program stops and runs again later.
    [Fact]
    public void Dismiss_TheProgramComesBack_StaysHidden()
    {
        _processes.Run("SignalRgb");
        _check.Check();
        _check.Dismiss("signalrgb");
        _processes.Processes.Clear();
        _check.Check();

        _processes.Run("SignalRgbService", RunningProcess.ServiceSessionId);
        _check.Check();

        Assert.Empty(_check.Shown);
        Assert.Equal([SignalRgb], _check.Found);
    }

    // Also a program that does not run now, such as from the list of an older message.
    [Fact]
    public void Dismiss_AProgramThatDoesNotRun_KeepsIt()
    {
        _check.Dismiss("razer");

        _processes.Run("RzSDKService", RunningProcess.ServiceSessionId);
        _check.Check();

        Assert.Empty(_check.Shown);
        Assert.Equal(["razer"], _check.Dismissed);
    }

    [Fact]
    public void Dismiss_Twice_TellsOnce()
    {
        _check.Dismiss("signalrgb");
        _check.Dismiss("signalrgb");

        Assert.Equal(1, _dismissedChanges);
    }

    [Fact]
    public void Dismiss_AnUnknownId_Throws()
    {
        Assert.Throws<ArgumentException>(() => _check.Dismiss("signal-rgb"));

        Assert.Empty(_check.Dismissed);
        Assert.Equal(0, _dismissedChanges);
    }

    // From settings.json: an id taken out by hand shows its message again. Ids it does not know,
    // such as of a newer version, stay, so the file keeps them.
    [Fact]
    public void Dismissed_SetWithoutAnId_ShowsItAgain()
    {
        _processes.Run("SignalRgb");
        _check.Dismissed = ["signalrgb", "future"];
        _check.Check();
        Assert.Empty(_check.Shown);

        _check.Dismissed = ["future"];

        Assert.Equal([SignalRgb], _check.Shown);
        Assert.Equal(["future"], _check.Dismissed);
        Assert.Equal(2, _dismissedChanges);
    }

    // An id written by hand in another case still hides its program, and the app's own id for it
    // is then no second one.
    [Fact]
    public void Dismissed_InAnotherCase_HidesItAndIsNotAddedTwice()
    {
        _processes.Run("SignalRgb");
        _check.Dismissed = ["SignalRGB"];
        _check.Check();
        var dismissedChanges = _dismissedChanges;

        _check.Dismiss("signalrgb");

        Assert.Empty(_check.Shown);
        Assert.Equal(["SignalRGB"], _check.Dismissed);
        Assert.Equal(dismissedChanges, _dismissedChanges);
    }

    [Fact]
    public void Dismissed_SetTheSame_TellsNothing()
    {
        _check.Dismissed = ["signalrgb", "icue"];
        var changes = _changes;

        _check.Dismissed = ["icue", "signalrgb", ""];

        Assert.Equal(changes, _changes);
        Assert.Equal(1, _dismissedChanges);
        Assert.Equal(["icue", "signalrgb"], _check.Dismissed);
    }
}
