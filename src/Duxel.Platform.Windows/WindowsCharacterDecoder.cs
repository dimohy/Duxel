using System.Text;

namespace Duxel.Platform.Windows;

/// <summary>Buffers one UTF-16 lead unit and emits only complete, non-control Unicode scalars.</summary>
internal struct WindowsCharacterDecoder
{
    private char _high;
    private int _highRepeat;
    internal readonly bool HasPendingHighSurrogate => _high != 0;

    internal bool TryDecode(uint value, out uint scalar)
        => TryDecode(value, 1, out scalar, out _);

    internal bool TryDecode(uint value, int repeat, out uint scalar, out int scalarRepeat)
    {
        scalar = 0;
        scalarRepeat = 0;
        var high = _high;
        var highRepeat = _highRepeat;
        _high = default;
        _highRepeat = default;
        if (value <= char.MaxValue)
        {
            var unit = (char)value;
            if (char.IsHighSurrogate(unit))
            {
                _high = unit;
                _highRepeat = repeat;
                return false;
            }
            if (char.IsLowSurrogate(unit))
            {
                if (high == 0)
                    return false;
                scalar = (uint)char.ConvertToUtf32(high, unit);
                scalarRepeat = highRepeat;
                return true;
            }
        }
        if (value > 0x10FFFF || !Rune.IsValid((int)value) || Rune.IsControl(new Rune((int)value)))
            return false;
        scalar = value;
        scalarRepeat = repeat;
        return true;
    }
}
