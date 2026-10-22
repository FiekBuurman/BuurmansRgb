using BuurmansRgb.Contracts;
using BuurmansRgb.Core;
using BuurmansRgb.Effects;
using BuurmansRgb.Engine.Lighting;

namespace BuurmansRgb.Engine.Tests;

public sealed class DeviceOutputTests
{
    private readonly RecordingDeviceProvider _provider = new();
    private readonly DeviceOutput _output;

    public DeviceOutputTests() => _output = new DeviceOutput(_provider, _provider.Device);

    [Fact]
    public void Render_ScalesTheEffectByTheBrightness()
    {
        _output.Show(new StaticEffect(Orange), 0.5, EffectSpeed.Default);

        _output.Render(TimeSpan.Zero);

        Assert.All(_output.Buffer, led => Assert.Equal(Orange.Scale(0.5), led));
    }

    [Fact]
    public void Render_FullBrightness_KeepsTheColor()
    {
        _output.Show(new StaticEffect(Orange), 1.0, EffectSpeed.Default);

        _output.Render(TimeSpan.Zero);

        Assert.All(_output.Buffer, led => Assert.Equal(Orange, led));
    }

    [Fact]
    public void Send_SendsTheBufferToTheProvider()
    {
        _output.Show(new StaticEffect(Orange), 1.0, EffectSpeed.Default);
        _output.Render(TimeSpan.Zero);

        _output.Send();

        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.0)]
    public void Send_Scaled_DimsWhatGoesOutButKeepsTheBuffer(double scale)
    {
        _output.Show(new StaticEffect(Orange), 1.0, EffectSpeed.Default);
        _output.Render(TimeSpan.Zero);

        _output.Send(scale);

        Assert.Equal([Orange.Scale(scale), Orange.Scale(scale), Orange.Scale(scale)], _provider.LastColors);
        Assert.All(_output.Buffer, led => Assert.Equal(Orange, led));
    }

    // What the device got was dimmed, so the real colors differ from it and go out.
    [Fact]
    public void SendIfChanged_AfterAScaledSend_SendsTheRealColors()
    {
        _output.Show(new StaticEffect(Orange), 1.0, EffectSpeed.Default);
        _output.Render(TimeSpan.Zero);
        _output.Send(0.0);

        Assert.True(_output.SendIfChanged());
        Assert.Equal([Orange, Orange, Orange], _provider.LastColors);
    }

    [Fact]
    public void SendIfChanged_SameColorsAsLastSent_SendsNothing()
    {
        _output.Show(new StaticEffect(Orange), 1.0, EffectSpeed.Default);
        _output.Render(TimeSpan.Zero);
        _output.Send();

        _output.Render(TimeSpan.Zero);

        Assert.False(_output.SendIfChanged());
        Assert.Equal(1, _provider.SetLedsCount);
    }

    [Fact]
    public void SendIfChanged_OtherColors_SendsThem()
    {
        _output.Show(new StaticEffect(Orange), 1.0, EffectSpeed.Default);
        _output.Render(TimeSpan.Zero);
        _output.Send();
        _output.Show(new StaticEffect(Blue), 1.0, EffectSpeed.Default);
        _output.Render(TimeSpan.Zero);

        Assert.True(_output.SendIfChanged());
        Assert.Equal([Blue, Blue, Blue], _provider.LastColors);
    }

    [Fact]
    public void IsAnimated_FollowsTheEffect()
    {
        Assert.False(_output.IsAnimated);

        _output.Show(new RainbowEffect(), 1.0, EffectSpeed.Default);

        Assert.True(_output.IsAnimated);
    }
}
