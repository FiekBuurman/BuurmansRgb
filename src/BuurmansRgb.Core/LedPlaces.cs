namespace BuurmansRgb.Core;

// Where the LEDs of a zone sit (LedPlace), for the engine, which lets an effect run in a direction,
// and for the preview, which draws them there, so both use the same places. A zone with a Matrix,
// such as the keys of a keyboard, places its LEDs by the matrix; the ring of a fan is a circle;
// any other zone is a line from its first LED to its last.
public static class LedPlaces
{
    // The most LEDs a single fan ring has; fans have a few dozen at most.
    public const int MaxRingLeds = 48;

    // Below this the LEDs of a zone lie square to a direction, as a line does across it.
    private const double Flat = 1e-9;

    // OpenRGB has no zone type for a ring, and zone names differ per controller ("Fan" on a
    // graphics card may well be a bar), so only the device type tells a fan. A zone whose length
    // can be set is a channel of a hub that OpenRGB also calls a cooler (a Lian Li hub, a Corsair
    // Commander), with a strip or several fans in a row on it, and so is a zone longer than one fan
    // ring; those stay a line.
    public static bool IsRing(DeviceType type, RgbZone zone, int ledCount)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return type == DeviceType.Fan && zone.Matrix is null && !zone.Resizable && ledCount <= MaxRingLeds;
    }

    // Where an LED of a ring of ledCount sits from the middle, on a circle of radius 1. OpenRGB
    // tells neither where a fan starts nor which way it counts, so the ring reads like a clock:
    // LED 0 at the top, then clockwise.
    public static (double Right, double Down) OnRing(int led, int ledCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(led);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(led, ledCount);
        var angle = 2.0 * Math.PI * led / ledCount;
        return (Math.Sin(angle), -Math.Cos(angle));
    }

    // The LEDs in reading order, so many per row; the last row may hold fewer.
    public static LedMatrix Wrap(int ledCount, int columns)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ledCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        var rows = (ledCount + columns - 1) / columns;
        var cells = new int[rows * columns];
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i] = i < ledCount ? i : LedMatrix.NoLed;
        }

        return new(rows, columns, cells);
    }

    // The positions of a matrix, such as the keys of a keyboard, for a zone of ledCount LEDs. A
    // cell that points past the zone stays empty; this is the one place that guards that, since
    // providers pass a key map on as they get it and a zone may hold fewer LEDs than its map.
    // LEDs that the matrix does not place follow in rows of their own below it, so every LED still
    // has a place.
    public static LedMatrix Place(LedMatrix matrix, int ledCount)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentOutOfRangeException.ThrowIfNegative(ledCount);
        if (matrix.Cells.Count == 0)
        {
            return Wrap(ledCount, Math.Max(ledCount, 1));
        }

        var placed = new bool[ledCount];
        List<int> cells = new(matrix.Cells.Count);
        foreach (var cell in matrix.Cells)
        {
            var led = cell >= 0 && cell < ledCount ? cell : LedMatrix.NoLed;
            if (led != LedMatrix.NoLed)
            {
                placed[led] = true;
            }

            cells.Add(led);
        }

        var rest = Enumerable.Range(0, ledCount).Where(led => !placed[led]).ToArray();
        var restRows = (rest.Length + matrix.Width - 1) / matrix.Width;
        for (var i = 0; i < restRows * matrix.Width; i++)
        {
            cells.Add(i < rest.Length ? rest[i] : LedMatrix.NoLed);
        }

        return new(matrix.Height + restRows, matrix.Width, cells);
    }

    // The zone of a part of RgbZone.PartsOf: the zone itself, or for the LEDs that no zone claims
    // (the part after the last zone, or a device without zones) a zone of their own, as the
    // preview shows them. Such a zone of a fan is a ring.
    public static RgbZone ZoneOfPart(IReadOnlyList<RgbZone> zones, int part, int ledCount)
    {
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentOutOfRangeException.ThrowIfNegative(part);
        return part < zones.Count ? zones[part] : RgbZone.Fixed(string.Empty, ledCount);
    }

    // The place of each of the ledCount LEDs of a zone. A matrix keeps its cells square; an LED
    // in more than one cell sits in the middle of them. A ring has its middle at 0.5, 0.5 and LED
    // 0 at the top. A line runs from left to right through the middle.
    public static LedPlace[] Of(DeviceType type, RgbZone zone, int ledCount)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentOutOfRangeException.ThrowIfNegative(ledCount);
        var places = new LedPlace[ledCount];
        if (zone.Matrix is { } matrix)
        {
            PlaceInMatrix(Place(matrix, ledCount), places);
        }
        else if (IsRing(type, zone, ledCount))
        {
            for (var led = 0; led < ledCount; led++)
            {
                var (right, down) = OnRing(led, ledCount);
                places[led] = ledCount == 1 ? new(0.5, 0.5) : new(0.5 + 0.5 * right, 0.5 + 0.5 * down);
            }
        }
        else
        {
            for (var led = 0; led < ledCount; led++)
            {
                places[led] = new((led + 0.5) / ledCount, 0.5);
            }
        }

        return places;
    }

    // The place of each LED along the way x·cos(degrees) + y·sin(degrees): 0 for the LED furthest
    // back, places.Length - 1 for the one furthest ahead, so a line at 0 degrees counts 0, 1, 2 as
    // its index does. 0 degrees runs to the right, 90 down, 180 to the left and 270 up. LEDs that
    // lie square to the direction, such as a line at 90 degrees, all sit in the middle.
    public static void StepsAlong(ReadOnlySpan<LedPlace> places, int degrees, Span<double> steps)
    {
        if (steps.Length != places.Length)
        {
            throw new ArgumentException("There is one step per place.", nameof(steps));
        }

        if (places.Length == 0)
        {
            return;
        }

        var angle = degrees * Math.PI / 180.0;
        var (cos, sin) = (Math.Cos(angle), Math.Sin(angle));
        var (low, high) = (double.MaxValue, double.MinValue);
        for (var i = 0; i < places.Length; i++)
        {
            steps[i] = places[i].X * cos + places[i].Y * sin;
            low = Math.Min(low, steps[i]);
            high = Math.Max(high, steps[i]);
        }

        var last = places.Length - 1;
        for (var i = 0; i < steps.Length; i++)
        {
            steps[i] = high - low < Flat ? last / 2.0 : (steps[i] - low) / (high - low) * last;
        }
    }

    // The place of every LED of one zone along a direction, from 0 up to 1, as an effect over that
    // zone alone takes i / ledCount: the LED furthest back at 0, the one furthest ahead at
    // (ledCount - 1) / ledCount.
    public static double[] Along(DeviceType type, RgbZone zone, int ledCount, int degrees)
    {
        var positions = new double[ledCount];
        StepsAlong(Of(type, zone, ledCount), degrees, positions);
        for (var i = 0; i < positions.Length; i++)
        {
            positions[i] /= ledCount;
        }

        return positions;
    }

    // The place of every LED of a device along a direction, from 0 up to 1, as an effect over the
    // device takes i / LedCount. The zones keep their share and their order, as by index: a zone
    // of the LEDs first to first + n runs from first / LedCount to (first + n - 1) / LedCount, but
    // in the direction. Where the zones sit from each other nobody tells, so each one gets its own
    // square.
    public static double[] Along(RgbDevice device, int degrees)
    {
        ArgumentNullException.ThrowIfNull(device);
        var positions = new double[device.LedCount];
        var parts = RgbZone.PartsOf(device.Zones, device.LedCount);
        for (var part = 0; part < parts.Length; part++)
        {
            var (first, count) = parts[part];
            var steps = positions.AsSpan(first, count);
            StepsAlong(Of(device.Type, ZoneOfPart(device.Zones, part, count), count), degrees, steps);
            for (var i = 0; i < steps.Length; i++)
            {
                steps[i] = (first + steps[i]) / device.LedCount;
            }
        }

        return positions;
    }

    private static void PlaceInMatrix(LedMatrix layout, LedPlace[] places)
    {
        var side = (double)Math.Max(layout.Width, layout.Height);
        var cells = new int[places.Length];
        var (sumX, sumY) = (new double[places.Length], new double[places.Length]);
        for (var cell = 0; cell < layout.Cells.Count; cell++)
        {
            var led = layout.Cells[cell];
            if (led == LedMatrix.NoLed)
            {
                continue;
            }

            cells[led]++;
            sumX[led] += cell % layout.Width + 0.5;
            sumY[led] += cell / layout.Width + 0.5;
        }

        for (var led = 0; led < places.Length; led++)
        {
            places[led] = new(sumX[led] / cells[led] / side, sumY[led] / cells[led] / side);
        }
    }
}
