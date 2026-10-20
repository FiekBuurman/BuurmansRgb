namespace BuurmansRgb.Core.Tests;

public sealed class RgbColorTests
{
    [Theory]
    [InlineData(1.0, 200, 100, 50)]
    [InlineData(0.5, 100, 50, 25)]
    [InlineData(0.0, 0, 0, 0)]
    [InlineData(1.5, 200, 100, 50)]
    [InlineData(-0.5, 0, 0, 0)]
    public void Scale_MultipliesEachChannelByClampedBrightness(double brightness, byte r, byte g, byte b)
    {
        var color = new RgbColor(200, 100, 50);

        Assert.Equal(new RgbColor(r, g, b), color.Scale(brightness));
    }

    [Theory]
    [InlineData(0.0, 255, 0, 0)]
    [InlineData(1.0 / 6.0, 255, 255, 0)]
    [InlineData(2.0 / 6.0, 0, 255, 0)]
    [InlineData(3.0 / 6.0, 0, 255, 255)]
    [InlineData(4.0 / 6.0, 0, 0, 255)]
    [InlineData(5.0 / 6.0, 255, 0, 255)]
    [InlineData(1.0, 255, 0, 0)]
    [InlineData(-1.0 / 6.0, 255, 0, 255)]
    public void FromHsv_FullSaturationAndValue_ReturnsPrimaryAndSecondaryColors(double hue, byte r, byte g, byte b)
    {
        Assert.Equal(new RgbColor(r, g, b), RgbColor.FromHsv(hue, 1.0, 1.0));
    }

    [Fact]
    public void FromHsv_ZeroSaturation_ReturnsGray()
    {
        Assert.Equal(new RgbColor(128, 128, 128), RgbColor.FromHsv(0.3, 0.0, 0.5));
    }

    [Fact]
    public void FromHsv_ZeroValue_ReturnsBlack()
    {
        Assert.Equal(RgbColor.Black, RgbColor.FromHsv(0.7, 1.0, 0.0));
    }

    [Theory]
    [InlineData(255, 0, 0, 0.0)]
    [InlineData(255, 255, 0, 1.0 / 6.0)]
    [InlineData(0, 255, 0, 2.0 / 6.0)]
    [InlineData(0, 255, 255, 3.0 / 6.0)]
    [InlineData(0, 0, 255, 4.0 / 6.0)]
    [InlineData(255, 0, 255, 5.0 / 6.0)]
    public void ToHsv_PrimaryAndSecondaryColors_ReturnsHueWithFullSaturationAndValue(byte r, byte g, byte b, double hue)
    {
        var (h, s, v) = new RgbColor(r, g, b).ToHsv();

        Assert.Equal(hue, h, precision: 6);
        Assert.Equal(1.0, s, precision: 6);
        Assert.Equal(1.0, v, precision: 6);
    }

    [Fact]
    public void ToHsv_Gray_ReturnsZeroHueAndSaturation()
    {
        Assert.Equal((0.0, 0.0, 128 / 255.0), new RgbColor(128, 128, 128).ToHsv());
    }

    [Theory]
    [InlineData(255, 136, 0)]
    [InlineData(10, 200, 128)]
    [InlineData(80, 40, 120)]
    [InlineData(200, 10, 60)]
    [InlineData(0, 0, 0)]
    public void ToHsv_RoundTripsThroughFromHsv(byte r, byte g, byte b)
    {
        var color = new RgbColor(r, g, b);
        var (h, s, v) = color.ToHsv();

        Assert.Equal(color, RgbColor.FromHsv(h, s, v));
    }

    [Theory]
    [InlineData(0.0, 0, 100, 200)]
    [InlineData(0.5, 100, 150, 100)]
    [InlineData(1.0, 200, 200, 0)]
    [InlineData(2.0, 200, 200, 0)]
    public void Lerp_InterpolatesEachChannel(double amount, byte r, byte g, byte b)
    {
        var from = new RgbColor(0, 100, 200);
        var to = new RgbColor(200, 200, 0);

        Assert.Equal(new RgbColor(r, g, b), RgbColor.Lerp(from, to, amount));
    }

    [Fact]
    public void ToHex_FormatsAsUppercaseHashRrGgBb()
    {
        Assert.Equal("#0AFF80", new RgbColor(10, 255, 128).ToHex());
    }

    [Theory]
    [InlineData("#0AFF80", 10, 255, 128)]
    [InlineData("0aff80", 10, 255, 128)]
    [InlineData("#000000", 0, 0, 0)]
    public void TryParseHex_ValidText_ReturnsColor(string text, byte r, byte g, byte b)
    {
        Assert.True(RgbColor.TryParseHex(text, out var color));
        Assert.Equal(new RgbColor(r, g, b), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#FFF")]
    [InlineData("#FF00FF00")]
    [InlineData("#GG0000")]
    [InlineData(" #FF0000")]
    [InlineData("#-F0000")]
    public void TryParseHex_InvalidText_ReturnsFalse(string text)
    {
        Assert.False(RgbColor.TryParseHex(text, out _));
    }

    [Fact]
    public void TryParseHex_RoundTripsToHex()
    {
        var color = new RgbColor(1, 2, 3);

        Assert.True(RgbColor.TryParseHex(color.ToHex(), out var parsed));
        Assert.Equal(color, parsed);
    }
}
