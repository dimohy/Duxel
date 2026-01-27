using System;
using System.Collections.Generic;
using Dux.Core;

namespace Dux.Core.Dsl;

public sealed class UiDslState
{
    private readonly Dictionary<string, bool> _bools = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _ints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, uint> _uints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _floats = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _doubles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiVector2> _vector2 = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiVector4> _vector4 = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UiColor> _colors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float[]> _floatArrays = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int[]> _intArrays = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double[]> _doubleArrays = new(StringComparer.Ordinal);

    public UiState UiState { get; } = new();

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

    public int GetInt(string key, int defaultValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_ints.TryGetValue(key, out var value))
        {
            return value;
        }

        _ints[key] = defaultValue;
        return defaultValue;
    }

    public void SetInt(string key, int value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _ints[key] = value;
    }

    public uint GetUInt(string key, uint defaultValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_uints.TryGetValue(key, out var value))
        {
            return value;
        }

        _uints[key] = defaultValue;
        return defaultValue;
    }

    public void SetUInt(string key, uint value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _uints[key] = value;
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_floats.TryGetValue(key, out var value))
        {
            return value;
        }

        _floats[key] = defaultValue;
        return defaultValue;
    }

    public void SetFloat(string key, float value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _floats[key] = value;
    }

    public double GetDouble(string key, double defaultValue = 0d)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_doubles.TryGetValue(key, out var value))
        {
            return value;
        }

        _doubles[key] = defaultValue;
        return defaultValue;
    }

    public void SetDouble(string key, double value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _doubles[key] = value;
    }

    public string GetString(string key, string defaultValue = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_strings.TryGetValue(key, out var value))
        {
            return value;
        }

        _strings[key] = defaultValue;
        return defaultValue;
    }

    public void SetString(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _strings[key] = value ?? string.Empty;
    }

    public UiVector2 GetVector2(string key, UiVector2 defaultValue = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_vector2.TryGetValue(key, out var value))
        {
            return value;
        }

        _vector2[key] = defaultValue;
        return defaultValue;
    }

    public void SetVector2(string key, UiVector2 value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _vector2[key] = value;
    }

    public UiVector4 GetVector4(string key, UiVector4 defaultValue = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_vector4.TryGetValue(key, out var value))
        {
            return value;
        }

        _vector4[key] = defaultValue;
        return defaultValue;
    }

    public void SetVector4(string key, UiVector4 value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _vector4[key] = value;
    }

    public UiColor GetColor(string key, UiColor defaultValue = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_colors.TryGetValue(key, out var value))
        {
            return value;
        }

        _colors[key] = defaultValue;
        return defaultValue;
    }

    public void SetColor(string key, UiColor value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _colors[key] = value;
    }

    public float[] GetFloatArray(string key, float[] defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(defaultValue);

        if (_floatArrays.TryGetValue(key, out var value))
        {
            return value;
        }

        _floatArrays[key] = defaultValue;
        return defaultValue;
    }

    public void SetFloatArray(string key, float[] value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        _floatArrays[key] = value;
    }

    public int[] GetIntArray(string key, int[] defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(defaultValue);

        if (_intArrays.TryGetValue(key, out var value))
        {
            return value;
        }

        _intArrays[key] = defaultValue;
        return defaultValue;
    }

    public void SetIntArray(string key, int[] value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        _intArrays[key] = value;
    }

    public double[] GetDoubleArray(string key, double[] defaultValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(defaultValue);

        if (_doubleArrays.TryGetValue(key, out var value))
        {
            return value;
        }

        _doubleArrays[key] = defaultValue;
        return defaultValue;
    }

    public void SetDoubleArray(string key, double[] value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        _doubleArrays[key] = value;
    }
}
