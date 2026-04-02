using Duxel.Core;

namespace Duxel.Core.Dsl;

/// <summary>
/// Convenience wrapper around <see cref="IUiDslValueSource"/> that allows
/// registering per-id typed values instead of implementing the interface directly.
/// Unregistered keys fall through (return false) so <see cref="UiDslState"/> handles them.
/// </summary>
public sealed class UiDslValueBinder : IUiDslValueSource
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

    // ── Fluent registration ──────────────────────────────

    public UiDslValueBinder BindBool(string id, bool initialValue = false)
    {
        _bools[id] = initialValue;
        return this;
    }

    public UiDslValueBinder BindInt(string id, int initialValue = 0)
    {
        _ints[id] = initialValue;
        return this;
    }

    public UiDslValueBinder BindUInt(string id, uint initialValue = 0)
    {
        _uints[id] = initialValue;
        return this;
    }

    public UiDslValueBinder BindFloat(string id, float initialValue = 0f)
    {
        _floats[id] = initialValue;
        return this;
    }

    public UiDslValueBinder BindDouble(string id, double initialValue = 0.0)
    {
        _doubles[id] = initialValue;
        return this;
    }

    public UiDslValueBinder BindString(string id, string initialValue = "")
    {
        _strings[id] = initialValue;
        return this;
    }

    public UiDslValueBinder BindVector2(string id, UiVector2 initialValue = default)
    {
        _vector2[id] = initialValue;
        return this;
    }

    public UiDslValueBinder BindVector4(string id, UiVector4 initialValue = default)
    {
        _vector4[id] = initialValue;
        return this;
    }

    public UiDslValueBinder BindColor(string id, UiColor initialValue = default)
    {
        _colors[id] = initialValue;
        return this;
    }

    // ── Convenience getters/setters ──────────────────────

    public bool GetBool(string id) => _bools.GetValueOrDefault(id);
    public int GetInt(string id) => _ints.GetValueOrDefault(id);
    public uint GetUInt(string id) => _uints.GetValueOrDefault(id);
    public float GetFloat(string id) => _floats.GetValueOrDefault(id);
    public double GetDouble(string id) => _doubles.GetValueOrDefault(id);
    public string GetString(string id) => _strings.GetValueOrDefault(id, "");
    public UiVector2 GetVector2(string id) => _vector2.GetValueOrDefault(id);
    public UiVector4 GetVector4(string id) => _vector4.GetValueOrDefault(id);
    public UiColor GetColor(string id) => _colors.GetValueOrDefault(id);

    public void SetBool(string id, bool value) => _bools[id] = value;
    public void SetInt(string id, int value) => _ints[id] = value;
    public void SetUInt(string id, uint value) => _uints[id] = value;
    public void SetFloat(string id, float value) => _floats[id] = value;
    public void SetDouble(string id, double value) => _doubles[id] = value;
    public void SetString(string id, string value) => _strings[id] = value;
    public void SetVector2(string id, UiVector2 value) => _vector2[id] = value;
    public void SetVector4(string id, UiVector4 value) => _vector4[id] = value;
    public void SetColor(string id, UiColor value) => _colors[id] = value;

    // ── IUiDslValueSource implementation ─────────────────

    bool IUiDslValueSource.TryGetBool(string id, out bool value) => _bools.TryGetValue(id, out value);
    void IUiDslValueSource.SetBool(string id, bool value) { if (_bools.ContainsKey(id)) _bools[id] = value; }

    bool IUiDslValueSource.TryGetInt(string id, out int value) => _ints.TryGetValue(id, out value);
    void IUiDslValueSource.SetInt(string id, int value) { if (_ints.ContainsKey(id)) _ints[id] = value; }

    bool IUiDslValueSource.TryGetUInt(string id, out uint value) => _uints.TryGetValue(id, out value);
    void IUiDslValueSource.SetUInt(string id, uint value) { if (_uints.ContainsKey(id)) _uints[id] = value; }

    bool IUiDslValueSource.TryGetFloat(string id, out float value) => _floats.TryGetValue(id, out value);
    void IUiDslValueSource.SetFloat(string id, float value) { if (_floats.ContainsKey(id)) _floats[id] = value; }

    bool IUiDslValueSource.TryGetDouble(string id, out double value) => _doubles.TryGetValue(id, out value);
    void IUiDslValueSource.SetDouble(string id, double value) { if (_doubles.ContainsKey(id)) _doubles[id] = value; }

    bool IUiDslValueSource.TryGetString(string id, out string value)
    {
        if (_strings.TryGetValue(id, out var v))
        {
            value = v;
            return true;
        }
        value = default!;
        return false;
    }
    void IUiDslValueSource.SetString(string id, string value) { if (_strings.ContainsKey(id)) _strings[id] = value; }

    bool IUiDslValueSource.TryGetVector2(string id, out UiVector2 value) => _vector2.TryGetValue(id, out value);
    void IUiDslValueSource.SetVector2(string id, UiVector2 value) { if (_vector2.ContainsKey(id)) _vector2[id] = value; }

    bool IUiDslValueSource.TryGetVector4(string id, out UiVector4 value) => _vector4.TryGetValue(id, out value);
    void IUiDslValueSource.SetVector4(string id, UiVector4 value) { if (_vector4.ContainsKey(id)) _vector4[id] = value; }

    bool IUiDslValueSource.TryGetColor(string id, out UiColor value) => _colors.TryGetValue(id, out value);
    void IUiDslValueSource.SetColor(string id, UiColor value) { if (_colors.ContainsKey(id)) _colors[id] = value; }
}
