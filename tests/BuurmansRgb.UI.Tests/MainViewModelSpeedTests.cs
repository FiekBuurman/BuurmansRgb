using BuurmansRgb.Contracts;
using BuurmansRgb.UI.Resources;
using BuurmansRgb.UI.ViewModels;

namespace BuurmansRgb.UI.Tests;

public sealed class MainViewModelSpeedTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 14, 5, 9, TimeSpan.Zero);

    private readonly FakeEngineConnector _connector = new();

    private FakeEngineApi Api => _connector.Connection.FakeApi;

    [Fact]
    public async Task Connect_ShowsTheGlobalSpeedAsPercentage()
    {
        var viewModel = await ConnectAsync(api => api.Lighting[""] = api.Lighting[""] with { Speed = 0.5 });

        Assert.Equal(50.0, viewModel.Speed);
        Assert.Empty(Api.Calls);
    }

    [Fact]
    public async Task Speed_Changed_SendsFactor()
    {
        var viewModel = await ConnectAsync();

        viewModel.Speed = 250;

        Assert.Equal(["speed all 2.50"], Api.Calls);
    }

    [Fact]
    public async Task Speed_ForOneDevice_SendsForThatDevice()
    {
        var viewModel = await ConnectAsync();
        viewModel.SelectedDevice = viewModel.Devices.Single(device => device.DeviceId == "fan");

        viewModel.Speed = 20;

        Assert.Equal(["speed fan 0.20"], Api.Calls);
    }

    [Theory]
    [InlineData(EffectIds.Static, false)]
    [InlineData(EffectIds.PerLed, false)]
    [InlineData(EffectIds.Rainbow, true)]
    [InlineData(EffectIds.Breathing, true)]
    [InlineData(EffectIds.Wave, true)]
    public async Task IsSpeedEnabled_OnlyForAnimatedEffects(string effectId, bool enabled)
    {
        var viewModel = await ConnectAsync();

        viewModel.SelectedEffect = EffectOption.Create(effectId);

        Assert.Equal(enabled, viewModel.IsSpeedEnabled);
    }

    [Fact]
    public async Task SelectedEffect_Changed_TellsTheSliderToEnable()
    {
        var viewModel = await ConnectAsync();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.SelectedEffect = EffectOption.Create(EffectIds.Rainbow);

        Assert.Contains(nameof(MainViewModel.IsSpeedEnabled), changed);
    }

    [Fact]
    public void SpeedRange_FollowsTheEngine()
    {
        Assert.Equal(EffectSpeed.Min * 100.0, MainViewModel.MinSpeed);
        Assert.Equal(EffectSpeed.Max * 100.0, MainViewModel.MaxSpeed);
    }

    [Fact]
    public async Task SaveStatus_BeforeAnySave_SaysItSavesByItself()
    {
        var viewModel = await ConnectAsync();

        Assert.Equal(Strings.SaveAutomatic, viewModel.SaveStatus);
    }

    [Fact]
    public async Task SaveStatus_EngineSaved_ShowsTheTime()
    {
        var viewModel = await ConnectAsync();

        Api.RaiseSaved(succeeded: true);

        Assert.Equal("Opgeslagen ✓ (14:05:09)", viewModel.SaveStatus);
    }

    [Fact]
    public async Task SaveStatus_SaveFailed_SaysChangesAreLostAfterARestart()
    {
        var viewModel = await ConnectAsync();

        Api.RaiseSaved(succeeded: false);

        Assert.StartsWith("Opslaan mislukt (14:05:09)", viewModel.SaveStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveStatus_AfterReconnect_OldConnectionIsIgnored()
    {
        var viewModel = await ConnectAsync();
        var oldApi = Api;
        await viewModel.ConnectCommand.ExecuteAsync(null);

        oldApi.RaiseSaved(succeeded: false);

        Assert.Equal(Strings.SaveAutomatic, viewModel.SaveStatus);
    }

    private async Task<MainViewModel> ConnectAsync(Action<FakeEngineApi>? arrange = null)
    {
        var viewModel = new MainViewModel(new ArrangingConnector(_connector, arrange ?? (_ => { })), new FixedTime(Now));
        await viewModel.ConnectCommand.ExecuteAsync(null);
        Assert.True(viewModel.IsConnected);
        return viewModel;
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
