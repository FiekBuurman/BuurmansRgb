using System.Collections;

namespace BuurmansRgb.UI.ViewModels;

// Some LEDs, counted from 0, in order and each once: a range from a click, Shift+click or a zone,
// or LEDs on their own from Ctrl+click. It never changes, so a view can keep and compare it, and
// Contains costs no allocation, as the preview asks it for every LED on every frame.
public sealed class LedSet : IReadOnlyList<int>, IEquatable<LedSet>
{
    private readonly int[] _leds;

    private LedSet(int[] leds) => _leds = leds;

    public static LedSet Empty { get; } = new([]);

    public int Count => _leds.Length;

    // The lowest and the highest LED; LedSelection.NoSelection for an empty set.
    public int First => _leds.Length == 0 ? LedSelection.NoSelection : _leds[0];

    public int Last => _leds.Length == 0 ? LedSelection.NoSelection : _leds[^1];

    // Whether it holds every LED from First to Last, without a gap.
    public bool IsRange => _leds.Length > 0 && Last - First + 1 == Count;

    public int this[int index] => _leds[index];

    // The LEDs first to last; empty when last comes before first.
    public static LedSet Range(int first, int last) =>
        last < first ? Empty : new([.. Enumerable.Range(first, last - first + 1)]);

    public static LedSet Of(IEnumerable<int> leds)
    {
        int[] sorted = [.. leds.Distinct().Order()];
        return sorted.Length == 0 ? Empty : new(sorted);
    }

    public bool Contains(int led) => Array.BinarySearch(_leds, led) >= 0;

    // The same LEDs with led added, or taken out when it was in.
    public LedSet Toggle(int led) => Of(Contains(led) ? _leds.Where(other => other != led) : _leds.Append(led));

    public LedSet Union(LedSet other) => Of(_leds.Concat(other._leds));

    // The LEDs from first to last, moved by offset: the part of a device that one zone shows.
    public LedSet Slice(int first, int last, int offset) =>
        Of(_leds.Where(led => led >= first && led <= last).Select(led => led + offset));

    public bool Equals(LedSet? other) => other is not null && _leds.AsSpan().SequenceEqual(other._leds);

    public override bool Equals(object? obj) => Equals(obj as LedSet);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var led in _leds)
        {
            hash.Add(led);
        }

        return hash.ToHashCode();
    }

    public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)_leds).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // As a test shows it when it fails: "{3, 5, 9}".
    public override string ToString() => $"{{{string.Join(", ", _leds)}}}";
}
