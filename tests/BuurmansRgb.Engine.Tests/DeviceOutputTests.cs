using BuurmansRgb.Core;
using BuurmansRgb.Effects;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class DeviceOutputTests
{
    private readonly RecordingDeviceProvider _provider = new();
    private readonly DeviceOutput _output;

    public DeviceOutputTests()
    {
        var device = Assert.Single(_provider.DiscoverAsync(TestContext.Current.CancellationToken).Result);
        _output = new DeviceOutput(_provider, device);
    }

    [Fact]
    public void Render_ScalesTheEffectByTheBrightness()
    {
        _output.Show(new StaticEffect(Orange), 0.5);

        _output.Render(TimeSpan.Zero);

        Assert.All(_output.Buffer, led => Assert.Equal(Orange.Scale(0.5), led));
    }

    [Fact]
    public void Render_FullBrightness_KeepsTheColor()
    {
        _output.Show(new StaticEffect(Orange), 1.0);

        _output.Render(TimeSpan.Zero);

        Assert.All(_output.Buffer, led => Assert.Equal(Orange, led));
    }

    [Fact]
    public void Send_SendsTheBufferToTheProvider()
    {
        _output.Show(new StaticEffect(Orange), 1.0);
        _output.Render(TimeSpan.Zero);

        _output.Send();

        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    [Fact]
    public void SendIfChanged_SameColorsAsLastSent_SendsNothing()
    {
        _output.Show(new StaticEffect(Orange), 1.0);
        _output.Render(TimeSpan.Zero);
        _output.Send();

        _output.Render(TimeSpan.Zero);

        Assert.False(_output.SendIfChanged());
        Assert.Equal(1, _provider.SetLedsCount);
    }

    [Fact]
    public void SendIfChanged_OtherColors_SendsThem()
    {
        _output.Show(new StaticEffect(Orange), 1.0);
        _output.Render(TimeSpan.Zero);
        _output.Send();
        _output.Show(new StaticEffect(Blue), 1.0);
        _output.Render(TimeSpan.Zero);

        Assert.True(_output.SendIfChanged());
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
    }

    [Fact]
    public void IsAnimated_FollowsTheEffect()
    {
        Assert.False(_output.IsAnimated);

        _output.Show(new RainbowEffect(), 1.0);

        Assert.True(_output.IsAnimated);
    }
}
