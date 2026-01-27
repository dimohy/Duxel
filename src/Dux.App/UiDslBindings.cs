using Dux.Core.Dsl;

namespace Dux.App;

public sealed class UiDslBindings : IUiDslEventSink, IUiDslValueSource
{
    private readonly Dictionary<string, Action> _buttonHandlers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<bool>> _checkboxHandlers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Func<bool> Get, Action<bool> Set)> _boolBindings = new(StringComparer.Ordinal);

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
}
