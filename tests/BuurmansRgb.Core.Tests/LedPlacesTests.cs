namespace BuurmansRgb.Core.Tests;

public sealed class LedPlacesTests
{
    private const int No = LedMatrix.NoLed;

    [Fact]
    public void Wrap_FillsRowsInReadingOrder()
    {
        Assert.Equal(new LedMatrix(2, 3, [0, 1, 2, 3, 4, No]), LedPlaces.Wrap(5, 3));
    }

    [Theory]
    [InlineData(0, 3, 0)]
    [InlineData(3, 3, 1)]
    [InlineData(4, 3, 2)]
    public void Wrap_HasRowsForEveryLed(int ledCount, int columns, int rows) =>
        Assert.Equal(rows, LedPlaces.Wrap(ledCount, columns).Height);

    [Fact]
    public void Wrap_WithoutColumns_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => LedPlaces.Wrap(5, 0));

    [Fact]
    public void Place_MatrixWithEveryLed_KeepsThePositions()
    {
        var keys = new LedMatrix(2, 3, [0, 1, 2, 3, No, 4]);

        Assert.Equal(keys, LedPlaces.Place(keys, 5));
    }

    [Fact]
    public void Place_CellPastTheZone_BecomesAGap()
    {
        var keys = new LedMatrix(1, 3, [0, 7, 1]);

        Assert.Equal(new LedMatrix(1, 3, [0, No, 1]), LedPlaces.Place(keys, 2));
    }

    // A keyboard whose logo LED is in the zone but not on the key map.
    [Fact]
    public void Place_LedsTheMatrixMisses_FollowInRowsBelow()
    {
        var keys = new LedMatrix(1, 2, [0, 2]);

        Assert.Equal(new LedMatrix(3, 2, [0, 2, 1, 3, 4, No]), LedPlaces.Place(keys, 5));
    }

    [Fact]
    public void Place_EmptyMatrix_ShowsTheLedsInOneRow()
    {
        Assert.Equal(new LedMatrix(1, 3, [0, 1, 2]), LedPlaces.Place(new LedMatrix(0, 0, []), 3));
    }

    [Fact]
    public void IsRing_FixedZoneOfAFan_IsARing() =>
        Assert.True(LedPlaces.IsRing(DeviceType.Fan, RgbZone.Fixed("Ring", 12), 12));

    [Fact]
    public void IsRing_OtherDeviceOrLongOrResizableZone_IsALine()
    {
        Assert.False(LedPlaces.IsRing(DeviceType.LedStrip, RgbZone.Fixed("Strip", 12), 12));
        Assert.False(LedPlaces.IsRing(DeviceType.Fan, RgbZone.Fixed("Fans", LedPlaces.MaxRingLeds + 1), LedPlaces.MaxRingLeds + 1));
        Assert.False(LedPlaces.IsRing(DeviceType.Fan, new RgbZone("Channel 1", 12, 0, 120), 12));
        Assert.False(LedPlaces.IsRing(DeviceType.Fan, RgbZone.Fixed("Keys", 2) with { Matrix = new LedMatrix(1, 2, [0, 1]) }, 2));
    }

    // LED 0 at the top, then clockwise: a quarter further is on the right, half way at the bottom.
    [Theory]
    [InlineData(0, 0.0, -1.0)]
    [InlineData(3, 1.0, 0.0)]
    [InlineData(6, 0.0, 1.0)]
    [InlineData(9, -1.0, 0.0)]
    public void OnRing_StartsAtTheTopAndGoesClockwise(int led, double right, double down)
    {
        var place = LedPlaces.OnRing(led, 12);

        Assert.Equal(right, place.Right, 9);
        Assert.Equal(down, place.Down, 9);
    }

    [Fact]
    public void Of_Line_RunsFromLeftToRightThroughTheMiddle() =>
        Assert.Equal([new(0.125, 0.5), new(0.375, 0.5), new(0.625, 0.5), new(0.875, 0.5)],
            LedPlaces.Of(DeviceType.LedStrip, RgbZone.Fixed("Strip", 4), 4));

    [Fact]
    public void Of_Ring_IsACircleInTheSquareWithLedZeroAtTheTop()
    {
        var places = LedPlaces.Of(DeviceType.Fan, RgbZone.Fixed("Ring", 4), 4);

        AssertPlace(new(0.5, 0.0), places[0]);
        AssertPlace(new(1.0, 0.5), places[1]);
        AssertPlace(new(0.5, 1.0), places[2]);
        AssertPlace(new(0.0, 0.5), places[3]);
    }

    [Fact]
    public void Of_RingOfOneLed_SitsInTheMiddle() =>
        Assert.Equal([new(0.5, 0.5)], LedPlaces.Of(DeviceType.Fan, RgbZone.Fixed("Ring", 1), 1));

    // Two rows of four cells: the cells stay square, so the rows end halfway down.
    [Fact]
    public void Of_Matrix_KeepsTheCellsSquare()
    {
        var zone = RgbZone.Fixed("Keys", 7) with { Matrix = new LedMatrix(2, 4, [0, 1, 2, 3, 4, 5, No, 6]) };

        var places = LedPlaces.Of(DeviceType.Keyboard, zone, 7);

        Assert.Equal(new LedPlace(0.125, 0.125), places[0]);
        Assert.Equal(new LedPlace(0.875, 0.125), places[3]);
        Assert.Equal(new LedPlace(0.125, 0.375), places[4]);
        Assert.Equal(new LedPlace(0.875, 0.375), places[6]);
    }

    // A space bar in three cells sits in the middle of them.
    [Fact]
    public void Of_LedInMoreCells_SitsInTheirMiddle()
    {
        var zone = RgbZone.Fixed("Keys", 2) with { Matrix = new LedMatrix(1, 4, [0, 1, 1, 1]) };

        Assert.Equal(new LedPlace(0.625, 0.125), LedPlaces.Of(DeviceType.Keyboard, zone, 2)[1]);
    }

    // The logo LED that the key map misses follows in a row below it, as the preview shows it.
    [Fact]
    public void Of_LedTheMatrixMisses_SitsInARowBelow()
    {
        var zone = RgbZone.Fixed("Keys", 3) with { Matrix = new LedMatrix(1, 2, [0, 1]) };

        Assert.Equal(new LedPlace(0.25, 0.75), LedPlaces.Of(DeviceType.Keyboard, zone, 3)[2]);
    }

    [Fact]
    public void StepsAlong_LineAtZero_CountsAsTheIndex()
    {
        var steps = new double[5];

        LedPlaces.StepsAlong(LedPlaces.Of(DeviceType.LedStrip, RgbZone.Fixed("Strip", 5), 5), 0, steps);

        Assert.Equal([0.0, 1.0, 2.0, 3.0, 4.0], steps, new Close());
    }

    [Fact]
    public void StepsAlong_LineAt180_CountsBackwards()
    {
        var steps = new double[5];

        LedPlaces.StepsAlong(LedPlaces.Of(DeviceType.LedStrip, RgbZone.Fixed("Strip", 5), 5), 180, steps);

        Assert.Equal([4.0, 3.0, 2.0, 1.0, 0.0], steps, new Close());
    }

    // A strip only goes forward or backward: at an angle it runs as at 0 degrees, and square to
    // it every LED sits in the middle.
    [Theory]
    [InlineData(45)]
    [InlineData(300)]
    public void StepsAlong_LineAtAnAngle_RunsForward(int degrees)
    {
        var steps = new double[5];

        LedPlaces.StepsAlong(LedPlaces.Of(DeviceType.LedStrip, RgbZone.Fixed("Strip", 5), 5), degrees, steps);

        Assert.Equal([0.0, 1.0, 2.0, 3.0, 4.0], steps, new Close());
    }

    [Theory]
    [InlineData(90)]
    [InlineData(270)]
    public void StepsAlong_LineSquareToTheDirection_SitsInTheMiddle(int degrees)
    {
        var steps = new double[5];

        LedPlaces.StepsAlong(LedPlaces.Of(DeviceType.LedStrip, RgbZone.Fixed("Strip", 5), 5), degrees, steps);

        Assert.Equal([2.0, 2.0, 2.0, 2.0, 2.0], steps);
    }

    // Two rows of two keys at 45 degrees: the top left first, the bottom right last, the other two
    // halfway.
    [Fact]
    public void StepsAlong_MatrixAtAnAngle_RunsDiagonally()
    {
        var zone = RgbZone.Fixed("Keys", 4) with { Matrix = new LedMatrix(2, 2, [0, 1, 2, 3]) };
        var steps = new double[4];

        LedPlaces.StepsAlong(LedPlaces.Of(DeviceType.Keyboard, zone, 4), 45, steps);

        Assert.Equal([0.0, 1.5, 1.5, 3.0], steps, new Close());
    }

    // A ring at 90 degrees runs from its top down: LED 0 first, the one opposite last.
    [Fact]
    public void StepsAlong_RingDown_RunsFromTheTop()
    {
        var steps = new double[4];

        LedPlaces.StepsAlong(LedPlaces.Of(DeviceType.Fan, RgbZone.Fixed("Ring", 4), 4), 90, steps);

        Assert.Equal([0.0, 1.5, 3.0, 1.5], steps, new Close());
    }

    [Fact]
    public void StepsAlong_OtherLength_Throws() =>
        Assert.Throws<ArgumentException>(() => LedPlaces.StepsAlong(new LedPlace[3], 0, new double[2]));

    [Fact]
    public void Along_ZoneAlone_RunsFromZeroUpToOne() =>
        Assert.Equal([0.75, 0.5, 0.25, 0.0], LedPlaces.Along(DeviceType.LedStrip, RgbZone.Fixed("Strip", 4), 4, 180), new Close());

    // Each zone keeps its share and place by index; the LEDs that no zone claims are a zone too.
    [Fact]
    public void Along_Device_KeepsTheShareOfEveryZone()
    {
        var device = new RgbDevice("board", "Board", DeviceType.Motherboard, [RgbZone.Fixed("A", 2), RgbZone.Fixed("B", 3)], 6)
        {
            Source = DeviceSource.Simulated,
        };

        var positions = LedPlaces.Along(device, 180);

        Assert.Equal([1 / 6.0, 0.0, 4 / 6.0, 3 / 6.0, 2 / 6.0, 5 / 6.0], positions, new Close());
    }

    [Fact]
    public void Along_DeviceAtZero_IsTheIndexOfALine()
    {
        var device = new RgbDevice("strip", "Strip", DeviceType.LedStrip, [RgbZone.Fixed("A", 3), RgbZone.Fixed("B", 5)])
        {
            Source = DeviceSource.Simulated,
        };

        Assert.Equal([.. Enumerable.Range(0, 8).Select(i => i / 8.0)], LedPlaces.Along(device, 0), new Close());
    }

    private static void AssertPlace(LedPlace expected, LedPlace actual)
    {
        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
    }

    private sealed class Close : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) < 1e-9;

        public int GetHashCode(double obj) => 0;
    }
}
