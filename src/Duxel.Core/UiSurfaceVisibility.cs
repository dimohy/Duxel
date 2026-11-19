namespace Duxel.Core;

/// <summary>Controlled/uncontrolled surface visibility and invocation origin shared by edge and centered portals.</summary>
internal sealed class UiSurfaceVisibility : IDisposable
{
    private readonly UiOverlayInputState _input;
    private readonly Action _invalidate;
    private bool? _pending;
    private UiControlActivationOrigin? _origin;
    internal UiBinding<bool>? Binding { get; private set; }
    internal Action<bool>? Changed { get; set; }
    internal bool DesiredOpen => _pending ?? Binding?.Value ?? _input.Open;
    internal UiSurfaceVisibility(UiOverlayInputState input, Action invalidate)
    {
        _input = input;
        _invalidate = invalidate;
        input.CanChangeOpen = RequestChange;
        input.Changed = open => Changed?.Invoke(open);
    }
    internal void SetBinding(UiBinding<bool>? value)
    {
        if (ReferenceEquals(Binding, value)) return;
        Binding = value;
        _invalidate();
    }
    private bool RequestChange(bool open)
    {
        if (Binding is not { } binding || binding.Value == open) return true;
        binding.Value = open;
        return binding.Value == open;
    }
    internal void Show(UiControlActivationOrigin? origin)
    {
        _pending = true;
        _origin = origin;
        _invalidate();
    }
    internal bool Close()
    {
        _pending = null;
        _origin = null;
        if (_input.Open && _input.Context is { } context) return context.RequestOverlayChange(_input, false, true);
        if (!RequestChange(false)) return false;
        _pending = false;
        _invalidate();
        return true;
    }
    internal (bool? Pending, UiControlActivationOrigin? Origin) TakePending()
    {
        var result = (_pending, _origin);
        _pending = null;
        _origin = null;
        return result;
    }
    public void Dispose()
    {
        Binding = null;
        Changed = null;
        _pending = null;
        _origin = null;
        _input.Dispose();
    }
}
