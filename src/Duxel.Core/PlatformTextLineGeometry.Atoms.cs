namespace Duxel.Core;

public sealed partial class PlatformTextLineGeometry
{
    internal PlatformTextLineGeometry WithAtomicRanges(ReadOnlySpan<(int Start, int Length)> ranges)
    {
        List<PlatformTextCharacterGeometry>? merged = null;
        foreach (var range in ranges)
        {
            if (range.Start < 0 || range.Length <= 0 || range.Start > Length - range.Length)
                throw new ArgumentOutOfRangeException(nameof(ranges));
            var characters = merged ?? (IReadOnlyList<PlatformTextCharacterGeometry>)_characters;
            var start = range.Start;
            var end = range.Start + range.Length;
            bool expanded;
            do
            {
                expanded = false;
                foreach (var character in characters)
                {
                    var characterEnd = character.Start + character.Length;
                    if (character.Start >= end || characterEnd <= start)
                        continue;
                    if (character.Start < start || characterEnd > end)
                    {
                        start = Math.Min(start, character.Start);
                        end = Math.Max(end, characterEnd);
                        expanded = true;
                    }
                }
            } while (expanded);
            var first = -1;
            var last = -1;
            for (var index = 0; index < characters.Count; index++)
                if (characters[index].Start < end
                    && characters[index].Start + characters[index].Length > start)
                {
                    if (first < 0)
                        first = index;
                    last = index;
                }
            if (first < 0 || first == last)
                continue;
            var left = float.PositiveInfinity;
            var right = float.NegativeInfinity;
            for (var index = first; index <= last; index++)
            {
                left = MathF.Min(left, characters[index].Left);
                right = MathF.Max(right, characters[index].Left + characters[index].Width);
            }
            var atom = new PlatformTextCharacterGeometry(start, end - start, left, right - left, characters[first].BidiLevel);
            merged ??= new(_characters);
            merged.RemoveRange(first, last - first + 1);
            merged.Insert(first, atom);
        }
        return merged is null ? this : new(Advance, _carets, merged.ToArray());
    }
}
