using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Dux.Core;

public sealed class UiTextBuffer
{
    private readonly StringBuilder _builder = new();

    public int Length => _builder.Length;

    public override string ToString() => _builder.ToString();

    public void Clear() => _builder.Clear();

    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _builder.Append(text);
    }

    public void Appendf(string format, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(format);
        _builder.AppendFormat(CultureInfo.InvariantCulture, format, args);
    }

    public void Appendfv(string format, IReadOnlyList<object?> args)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(args);
        _builder.AppendFormat(CultureInfo.InvariantCulture, format, args);
    }

    public IReadOnlyList<string> Split(char separator)
    {
        if (_builder.Length == 0)
        {
            return Array.Empty<string>();
        }

        return _builder.ToString().Split(separator, StringSplitOptions.RemoveEmptyEntries);
    }
}
