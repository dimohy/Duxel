using System;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool DeleteChars(ref string value, int pos, int count)
    {
        value ??= string.Empty;
        if (count <= 0)
        {
            return false;
        }

        if (pos < 0 || pos > value.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(pos));
        }

        var clamped = Math.Min(count, value.Length - pos);
        if (clamped <= 0)
        {
            return false;
        }

        value = ReplaceRange(value, pos, clamped, string.Empty);
        return true;
    }

    public bool InsertChars(ref string value, int pos, string text)
    {
        value ??= string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (pos < 0 || pos > value.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(pos));
        }

        value = ReplaceRange(value, pos, 0, text);
        return true;
    }
}

