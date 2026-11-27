using BuurmansRgb.UI.Services;

namespace BuurmansRgb.UI.Tests;

// What a MainViewModel without a window does: the safe answers, pinned here because every test that
// does not care about dialogs relies on them (FakeDialogs falls back on them).
public sealed class NoDialogsTests
{
    [Fact]
    public void NoFileIsChosen()
    {
        var dialogs = NoDialogs.Instance;

        Assert.Null(dialogs.ChooseBackupToSave("voorstel.zip"));
        Assert.Null(dialogs.ChooseBackupToRestore());
        Assert.Null(dialogs.ChooseShareToSave("Delen", "voorstel.buurmansrgb"));
        Assert.Null(dialogs.ChooseShareToImport());
        Assert.Null(dialogs.ChooseProgram());
    }

    [Fact]
    public void ADeleteOrUpdateGoesWithoutAQuestion()
    {
        var dialogs = NoDialogs.Instance;

        Assert.True(dialogs.ConfirmDeleteScene("Avond"));
        Assert.True(dialogs.ConfirmUpdateScene("Avond"));
        Assert.True(dialogs.ConfirmDeleteCustomEffect("Disco"));
    }

    // A restore replaces every setting, so without someone to ask it is not done.
    [Fact]
    public void ARestoreIsNotDoneWithoutAQuestion() =>
        Assert.False(NoDialogs.Instance.ConfirmRestore("oud.zip"));

    [Fact]
    public void FakeDialogs_WithoutAnAnswer_DoesTheSame()
    {
        IConfirmDialogs fake = new FakeDialogs();

        Assert.True(fake.ConfirmDeleteScene("Avond"));
        Assert.True(fake.ConfirmUpdateScene("Avond"));
        Assert.True(fake.ConfirmDeleteCustomEffect("Disco"));
        Assert.False(fake.ConfirmRestore("oud.zip"));
    }
}
