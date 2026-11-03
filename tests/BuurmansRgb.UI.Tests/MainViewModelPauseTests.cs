using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class MainViewModelPauseTests
{
    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task Connect_EffectsRun_ShowsNoMessage()
    {
        var viewModel = await ConnectAsync(_ => { });

        Assert.False(viewModel.EffectsPause.IsPaused);
    }

    [Fact]
    public async Task Connect_ShowsThePauseAndFollowsIt()
    {
        var viewModel = await ConnectAsync(api => api.Pause = PauseReasons.Manual);
        Assert.Equal(Strings.EffectsPausedManual, viewModel.EffectsPause.Text);

        Api.RaisePause(PauseReasons.Locked);
        Assert.Equal(Strings.EffectsPausedLocked, viewModel.EffectsPause.Text);
        Assert.False(viewModel.EffectsPause.CanResume);

        Api.RaisePause(PauseReasons.None);
        Assert.False(viewModel.EffectsPause.IsPaused);
        Assert.Equal(Strings.EffectsResumed, viewModel.EffectsPause.Text);
    }

    [Fact]
    public async Task Connect_PauseEventOvertakesTheAnswer_KeepsTheNewerPause()
    {
        var viewModel = await ConnectAsync(api =>
        {
            api.Pause = PauseReasons.Manual;
            api.PauseChangeWhileAnswering = PauseReasons.None;
        });

        Assert.False(viewModel.EffectsPause.IsPaused);
    }

    // The line above the preview follows the pause as the message at the top does, and
    // "Hervatten" there resumes the same way.
    [Fact]
    public async Task Connect_ThePreviewSaysWhyKnipperenIsGreyAndResumesFromThere()
    {
        var viewModel = await ConnectAsync(api => api.Pause = PauseReasons.Manual);
        Assert.Equal(Strings.PreviewActionsPausedManual, viewModel.Preview.ActionsHint.Text);
        Assert.True(viewModel.Preview.ActionsHint.IsShown);
        Assert.False(viewModel.Preview.CanFlash);

        viewModel.Preview.ActionsHint.ResumeCommand.Execute(null);

        Assert.Contains("resume", Api.Calls);
        Assert.False(viewModel.Preview.ActionsHint.IsShown);
        Assert.True(viewModel.Preview.CanFlash);
    }

    [Fact]
    public async Task Resume_SendsItAfterTheWaitingChangesAndTheMessageFollowsTheEngine()
    {
        var viewModel = await ConnectAsync(api => api.Pause = PauseReasons.Manual | PauseReasons.Locked);
        viewModel.Brightness = 40;

        viewModel.EffectsPause.ResumeCommand.Execute(null);

        Assert.Equal(["brightness all 0.40", "resume"], Api.Calls);
        Assert.Equal(PauseReasons.Locked, viewModel.EffectsPause.Reasons);
        Assert.Equal(Strings.EffectsPausedLocked, viewModel.EffectsPause.Text);
    }

    [Fact]
    public async Task Disconnected_ShowsNoPauseAndStopsFollowingIt()
    {
        var viewModel = await ConnectAsync(api => api.Pause = PauseReasons.Manual);
        var api = Api;

        _connector.Connection.Disconnect();

        Assert.False(viewModel.EffectsPause.IsPaused);
        Assert.False(viewModel.Preview.ActionsHint.IsShown);
        Assert.False(api.HasPauseSubscribers);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi> arrange)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, arrange));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
