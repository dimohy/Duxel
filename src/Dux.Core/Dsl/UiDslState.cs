using System;
using System.Collections.Generic;

namespace Dux.Core.Dsl;

public sealed class UiDslState
{
    private readonly Dictionary<string, bool> _bools = new(StringComparer.Ordinal);

    public bool GetBool(string key, bool defaultValue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_bools.TryGetValue(key, out var value))
        {
            return value;
        }

        _bools[key] = defaultValue;
        return defaultValue;
    }

    public void SetBool(string key, bool value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _bools[key] = value;
    }

    public bool ToggleBool(string key, bool defaultValue = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var next = !GetBool(key, defaultValue);
        _bools[key] = next;
        return next;
    }
}
