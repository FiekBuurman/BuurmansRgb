using BuurmansRgb.UI.Services;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

// The view model of the settings app, connected to the fake engine, for the tests of the view
// model and of the pages.
internal static class ConnectedViewModel
{
    // A view model connected to the fake engine of connector, or of a new one, which arrange
    // prepares before the view model loads from it, also when it connects again. "Kies uit wat nu
    // draait" lists programs, or nothing, but never what runs on this pc, and what Instellingen
    // opens (the log, the guide at guideAddress) goes into opened instead of opening on this pc.
    // animationEffects is "Animatie-effecten" of Windows, on without it. dialogs answers the file
    // dialogs and the questions; without it no file is chosen and nothing is asked (NoDialogs).
    internal static async Task<MainViewModel> CreateAsync(
        Action<FakeEngineApi>? arrange = null,
        FakeEngineConnector? connector = null,
        TimeProvider? time = null,
        IReadOnlyList<RunningProgram>? programs = null,
        List<string>? opened = null,
        string? guideAddress = AppInfoViewModel.GuideAddress,
        IAnimationEffects? animationEffects = null,
        IProcessMeter? processMeter = null,
        IClipboard? clipboard = null,
        FakeDialogs? dialogs = null)
    {
        connector ??= new FakeEngineConnector();
        var viewModel = new MainViewModel(
            arrange is null ? connector : new ArrangingConnector(connector, arrange),
            time,
            findPrograms: () => Task.FromResult(programs ?? []),
            appInfo: AppInfoViewModel.ForThisApp(target => opened?.Add(target), guideAddress),
            animationEffects: animationEffects,
            processMeter: processMeter,
            clipboard: clipboard,
            fileDialogs: dialogs,
            confirmDialogs: dialogs);
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }
}
