namespace Duxel.Core.Dsl;

/// <summary>
/// Convenience wrapper around <see cref="IUiDslEventSink"/> that allows
/// registering per-id callbacks instead of implementing the interface directly.
/// </summary>
public sealed class UiDslEventBinder : IUiDslEventSink
{
    private readonly Dictionary<string, Action> _buttonHandlers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<bool>> _checkboxHandlers = new(StringComparer.Ordinal);
    private Action<string>? _defaultButtonHandler;
    private Action<string, bool>? _defaultCheckboxHandler;

    /// <summary>Registers a handler for a specific button/menu-item/arrow-button id.</summary>
    public UiDslEventBinder Bind(string id, Action handler)
    {
        _buttonHandlers[id] = handler;
        return this;
    }

    /// <summary>Registers a handler for a specific checkbox id.</summary>
    public UiDslEventBinder BindCheckbox(string id, Action<bool> handler)
    {
        _checkboxHandlers[id] = handler;
        return this;
    }

    /// <summary>Registers a catch-all handler for any unmatched button event.</summary>
    public UiDslEventBinder OnAnyButton(Action<string> handler)
    {
        _defaultButtonHandler = handler;
        return this;
    }

    /// <summary>Registers a catch-all handler for any unmatched checkbox event.</summary>
    public UiDslEventBinder OnAnyCheckbox(Action<string, bool> handler)
    {
        _defaultCheckboxHandler = handler;
        return this;
    }

    void IUiDslEventSink.OnButton(string id)
    {
        if (_buttonHandlers.TryGetValue(id, out var handler))
            handler();
        else
            _defaultButtonHandler?.Invoke(id);
    }

    void IUiDslEventSink.OnCheckbox(string id, bool value)
    {
        if (_checkboxHandlers.TryGetValue(id, out var handler))
            handler(value);
        else
            _defaultCheckboxHandler?.Invoke(id, value);
    }
}
