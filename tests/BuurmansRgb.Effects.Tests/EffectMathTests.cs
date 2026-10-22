namespace BuurmansRgb.Effects.Tests;

public sealed class EffectMathTests
{
    [Theory]
    [InlineData(0.25, 0.25)]
    [InlineData(2.75, 0.75)]
    [InlineData(-0.25, 0.75)]
    [InlineData(-2.0, 0.0)]
    public void Fraction_IsThePartAfterTheWholeNumber(double value, double fraction)
    {
        Assert.Equal(fraction, EffectMath.Fraction(value), 12);
    }

    // Just below zero, value - Math.Floor(value) rounds to exactly 1.
    [Theory]
    [InlineData(-1e-17)]
    [InlineData(-1e-20)]
    [InlineData(-double.Epsilon)]
    public void Fraction_JustBelowZero_StaysBelowOne(double value)
    {
        Assert.InRange(EffectMath.Fraction(value), 0.0, Math.BitDecrement(1.0));
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1.0, 1.0)]
    public void SmoothStep_RunsFromZeroToOne(double t, double expected)
    {
        Assert.Equal(expected, EffectMath.SmoothStep(t), 12);
    }

    [Theory]
    [InlineData(0.0, 2.0)]
    [InlineData(0.25, 2.75)]
    [InlineData(1.0, 5.0)]
    public void Lerp_RunsFromTheFirstToTheSecond(double amount, double expected)
    {
        Assert.Equal(expected, EffectMath.Lerp(2.0, 5.0, amount), 12);
    }

    [Theory]
    [InlineData(-0.1, 0.0)]
    [InlineData(0.0, 1.0)]
    [InlineData(2.0, 0.25)]
    [InlineData(4.0, 0.0)]
    [InlineData(5.0, 0.0)]
    public void Tail_FadesOutBehindThePoint(double behind, double expected)
    {
        Assert.Equal(expected, EffectMath.Tail(behind, 4.0), 12);
    }

    // Of a tail of length 0, as the scanner's at an end, nothing shows.
    [Fact]
    public void Tail_OfNoLength_IsDark()
    {
        Assert.Equal(0.0, EffectMath.Tail(0.0, 0.0));
    }

    [Fact]
    public void Random_SameInputs_GiveTheSameNumber()
    {
        Assert.Equal(EffectMath.Random(7, 123_456_789_012, 3), EffectMath.Random(7, 123_456_789_012, 3));
    }

    [Fact]
    public void Random_IsSpreadEvenlyFromZeroUpToOne()
    {
        var count = 0;
        var sum = 0.0;
        var buckets = new int[10];
        for (var led = 0; led < 100; led++)
        {
            for (var step = -50L; step < 50; step++)
            {
                var value = EffectMath.Random(led, step, 1);
                Assert.InRange(value, 0.0, 0.99999999);
                buckets[(int)(value * 10)]++;
                sum += value;
                count++;
            }
        }

        Assert.InRange(sum / count, 0.49, 0.51);
        Assert.All(buckets, bucket => Assert.InRange(bucket, 900, 1100));
    }

    // Neighbouring LEDs, steps and streams, and steps that differ only in their upper half, must not
    // follow each other.
    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, 1L << 32, 0)]
    [InlineData(0, 0, 1)]
    public void Random_OtherInputs_GiveOtherNumbers(int ledShift, long stepShift, uint streamShift)
    {
        var same = 0;
        for (var i = 0; i < 1000; i++)
        {
            if (Math.Abs(EffectMath.Random(i, i, 5) - EffectMath.Random(i + ledShift, i + stepShift, 5 + streamShift)) < 0.01)
            {
                same++;
            }
        }

        // Two independent numbers lie within 0.01 of each other about 2 % of the time.
        Assert.InRange(same, 0, 50);
    }
}
