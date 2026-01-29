using System;
using System.Collections.Generic;
using System.Text;

namespace Duxel.Core;

public sealed class UiFontGlyphRangesBuilder
{
    private readonly bool[] _used = new bool[0x10000];

    public static UiFontGlyphRangesBuilder Create() => new();

    public void AddChar(int codepoint)
    {
        if ((uint)codepoint >= 0x10000u)
        {
            return;
        }

        _used[codepoint] = true;
    }

    public void AddText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value <= 0xFFFF)
            {
                _used[rune.Value] = true;
            }
        }
    }

    public void AddRange(int start, int end)
    {
        if (end < start)
        {
            return;
        }

        var clampedStart = Math.Max(0, start);
        var clampedEnd = Math.Min(0xFFFF, end);

        for (var i = clampedStart; i <= clampedEnd; i++)
        {
            _used[i] = true;
        }
    }

    public void AddRanges(IEnumerable<UiFontRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        foreach (var range in ranges)
        {
            AddRange(range.Start, range.End);
        }
    }

    public List<UiFontRange> BuildRanges()
    {
        var ranges = new List<UiFontRange>();
        var rangeStart = -1;

        for (var i = 0; i < _used.Length; i++)
        {
            if (_used[i])
            {
                if (rangeStart < 0)
                {
                    rangeStart = i;
                }
            }
            else if (rangeStart >= 0)
            {
                ranges.Add(new UiFontRange(rangeStart, i - 1));
                rangeStart = -1;
            }
        }

        if (rangeStart >= 0)
        {
            ranges.Add(new UiFontRange(rangeStart, _used.Length - 1));
        }

        return ranges;
    }

    public List<int> BuildCodepoints()
    {
        var codepoints = new List<int>();
        for (var i = 0; i < _used.Length; i++)
        {
            if (_used[i])
            {
                codepoints.Add(i);
            }
        }

        return codepoints;
    }
}

