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
}
