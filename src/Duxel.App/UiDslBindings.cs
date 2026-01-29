using Duxel.Core;
using Duxel.Core.Dsl;

namespace Duxel.App;

public sealed class UiDslBindings : IUiDslEventSink, IUiDslValueSource
{
    private readonly Dictionary<string, Action> _buttonHandlers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<bool>> _checkboxHandlers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Func<bool> Get, Action<bool> Set)> _boolBindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Func<int> Get, Action<int> Set)> _intBindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Func<uint> Get, Action<uint> Set)> _uintBindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Func<float> Get, Action<float> Set)> _floatBindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Func<double> Get, Action<double> Set)> _doubleBindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Func<string> Get, Action<string> Set)> _stringBindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Func<UiVector2> Get, Action<UiVector2> Set)> _vector2Bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Func<UiVector4> Get, Action<UiVector4> Set)> _vector4Bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Func<UiColor> Get, Action<UiColor> Set)> _colorBindings = new(StringComparer.Ordinal);

    public UiDslBindings BindButton(string id, Action handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(handler);
        _buttonHandlers[id] = handler;
        return this;
    }

    public UiDslBindings BindCheckbox(string id, Action<bool> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(handler);
        _checkboxHandlers[id] = handler;
        return this;
    }

    public UiDslBindings BindBool(string id, Func<bool> getter, Action<bool> setter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);
        _boolBindings[id] = (getter, setter);
        return this;
    }

    public UiDslBindings BindInt(string id, Func<int> getter, Action<int> setter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);
        _intBindings[id] = (getter, setter);
        return this;
    }

    public UiDslBindings BindUInt(string id, Func<uint> getter, Action<uint> setter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);
        _uintBindings[id] = (getter, setter);
        return this;
    }

    public UiDslBindings BindFloat(string id, Func<float> getter, Action<float> setter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);
        _floatBindings[id] = (getter, setter);
        return this;
    }

    public UiDslBindings BindDouble(string id, Func<double> getter, Action<double> setter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);
        _doubleBindings[id] = (getter, setter);
        return this;
    }

    public UiDslBindings BindString(string id, Func<string> getter, Action<string> setter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);
        _stringBindings[id] = (getter, setter);
        return this;
    }

    public UiDslBindings BindVector2(string id, Func<UiVector2> getter, Action<UiVector2> setter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);
        _vector2Bindings[id] = (getter, setter);
        return this;
    }

    public UiDslBindings BindVector4(string id, Func<UiVector4> getter, Action<UiVector4> setter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);
        _vector4Bindings[id] = (getter, setter);
        return this;
    }

    public UiDslBindings BindColor(string id, Func<UiColor> getter, Action<UiColor> setter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(setter);
        _colorBindings[id] = (getter, setter);
        return this;
    }

    void IUiDslEventSink.OnButton(string id)
    {
        if (_buttonHandlers.TryGetValue(id, out var handler))
        {
            handler();
        }
    }

    void IUiDslEventSink.OnCheckbox(string id, bool value)
    {
        if (_checkboxHandlers.TryGetValue(id, out var handler))
        {
            handler(value);
        }
    }

    bool IUiDslValueSource.TryGetBool(string id, out bool value)
    {
        if (_boolBindings.TryGetValue(id, out var binding))
        {
            value = binding.Get();
            return true;
        }

        value = false;
        return false;
    }

    void IUiDslValueSource.SetBool(string id, bool value)
    {
        if (_boolBindings.TryGetValue(id, out var binding))
        {
            binding.Set(value);
        }
    }

    bool IUiDslValueSource.TryGetInt(string id, out int value)
    {
        if (_intBindings.TryGetValue(id, out var binding))
        {
            value = binding.Get();
            return true;
        }

        value = 0;
        return false;
    }

    void IUiDslValueSource.SetInt(string id, int value)
    {
        if (_intBindings.TryGetValue(id, out var binding))
        {
            binding.Set(value);
        }
    }

    bool IUiDslValueSource.TryGetUInt(string id, out uint value)
    {
        if (_uintBindings.TryGetValue(id, out var binding))
        {
            value = binding.Get();
            return true;
        }

        value = 0;
        return false;
    }

    void IUiDslValueSource.SetUInt(string id, uint value)
    {
        if (_uintBindings.TryGetValue(id, out var binding))
        {
            binding.Set(value);
        }
    }

    bool IUiDslValueSource.TryGetFloat(string id, out float value)
    {
        if (_floatBindings.TryGetValue(id, out var binding))
        {
            value = binding.Get();
            return true;
        }

        value = 0f;
        return false;
    }

    void IUiDslValueSource.SetFloat(string id, float value)
    {
        if (_floatBindings.TryGetValue(id, out var binding))
        {
            binding.Set(value);
        }
    }

    bool IUiDslValueSource.TryGetDouble(string id, out double value)
    {
        if (_doubleBindings.TryGetValue(id, out var binding))
        {
            value = binding.Get();
            return true;
        }

        value = 0d;
        return false;
    }

    void IUiDslValueSource.SetDouble(string id, double value)
    {
        if (_doubleBindings.TryGetValue(id, out var binding))
        {
            binding.Set(value);
        }
    }

    bool IUiDslValueSource.TryGetString(string id, out string value)
    {
        if (_stringBindings.TryGetValue(id, out var binding))
        {
            value = binding.Get();
            return true;
        }

        value = string.Empty;
        return false;
    }

    void IUiDslValueSource.SetString(string id, string value)
    {
        if (_stringBindings.TryGetValue(id, out var binding))
        {
            binding.Set(value);
        }
    }

    bool IUiDslValueSource.TryGetVector2(string id, out UiVector2 value)
    {
        if (_vector2Bindings.TryGetValue(id, out var binding))
        {
            value = binding.Get();
            return true;
        }

        value = default;
        return false;
    }

    void IUiDslValueSource.SetVector2(string id, UiVector2 value)
    {
        if (_vector2Bindings.TryGetValue(id, out var binding))
        {
            binding.Set(value);
        }
    }

    bool IUiDslValueSource.TryGetVector4(string id, out UiVector4 value)
    {
        if (_vector4Bindings.TryGetValue(id, out var binding))
        {
            value = binding.Get();
            return true;
        }

        value = default;
        return false;
    }

    void IUiDslValueSource.SetVector4(string id, UiVector4 value)
    {
        if (_vector4Bindings.TryGetValue(id, out var binding))
        {
            binding.Set(value);
        }
    }

    bool IUiDslValueSource.TryGetColor(string id, out UiColor value)
    {
        if (_colorBindings.TryGetValue(id, out var binding))
        {
            value = binding.Get();
            return true;
        }

        value = default;
        return false;
    }

    void IUiDslValueSource.SetColor(string id, UiColor value)
    {
        if (_colorBindings.TryGetValue(id, out var binding))
        {
            binding.Set(value);
        }
    }
}

