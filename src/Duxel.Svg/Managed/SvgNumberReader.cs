using System.Globalization;

namespace Duxel.Svg.Managed;

internal ref struct SvgNumberReader(ReadOnlySpan<char> text)
{
    private ReadOnlySpan<char> _remaining = text;
    internal bool End { get { SkipWhitespace(); return _remaining.IsEmpty; } }
    internal char Peek { get { SkipWhitespace(); return _remaining.IsEmpty ? '\0' : _remaining[0]; } }
    internal char Take()
    {
        SkipWhitespace();
        if (_remaining.IsEmpty) throw new InvalidDataException("Unexpected end of SVG number list.");
        var value = _remaining[0];
        _remaining = _remaining[1..];
        return value;
    }
    private void SkipWhitespace()
    {
        while (!_remaining.IsEmpty && char.IsWhiteSpace(_remaining[0])) _remaining = _remaining[1..];
    }
    internal float Number()
    {
        SkipWhitespace();
        if (!_remaining.IsEmpty && _remaining[0] == ',')
        {
            _remaining = _remaining[1..];
            SkipWhitespace();
        }
        var length = 0;
        if (length < _remaining.Length && _remaining[length] is '+' or '-') length++;
        var digits = 0;
        while (length < _remaining.Length && char.IsAsciiDigit(_remaining[length])) { length++; digits++; }
        if (length < _remaining.Length && _remaining[length] == '.')
        {
            length++;
            while (length < _remaining.Length && char.IsAsciiDigit(_remaining[length])) { length++; digits++; }
        }
        if (digits == 0) throw new InvalidDataException("An SVG number requires decimal digits.");
        if (length < _remaining.Length && _remaining[length] is 'e' or 'E')
        {
            length++;
            if (length < _remaining.Length && _remaining[length] is '+' or '-') length++;
            var exponent = length;
            while (length < _remaining.Length && char.IsAsciiDigit(_remaining[length])) length++;
            if (length == exponent) throw new InvalidDataException("Incomplete SVG number exponent.");
        }
        if (!float.TryParse(_remaining[..length], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !float.IsFinite(result))
            throw new InvalidDataException("SVG numbers must be finite.");
        _remaining = _remaining[length..];
        return result;
    }
    internal bool Flag()
    {
        SkipWhitespace();
        if (!_remaining.IsEmpty && _remaining[0] == ',') _remaining = _remaining[1..];
        return Take() switch
        {
            '0' => false,
            '1' => true,
            _ => throw new InvalidDataException("SVG arc flags must be 0 or 1.")
        };
    }
}
