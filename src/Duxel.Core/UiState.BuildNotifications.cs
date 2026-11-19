namespace Duxel.Core;

public sealed partial class UiState<T>
{
    private List<WeakReference<IUiBuildObserver>>? _buildObservers;
    private int _notificationDepth;

    void IUiBuildState.Subscribe(IUiBuildObserver observer)
    {
        _buildObservers ??= [];
        WeakReference<IUiBuildObserver>? reusable = null;
        foreach (var weak in _buildObservers)
        {
            if (!weak.TryGetTarget(out var current)) reusable ??= weak;
            else if (ReferenceEquals(current, observer)) return;
        }
        if (reusable is not null) reusable.SetTarget(observer);
        else _buildObservers.Add(new(observer));
    }

    void IUiBuildState.Unsubscribe(IUiBuildObserver observer)
    {
        if (_buildObservers is null) return;
        for (var i = _buildObservers.Count - 1; i >= 0; i--)
        {
            if (!_buildObservers[i].TryGetTarget(out var current) || ReferenceEquals(current, observer))
            {
                if (_notificationDepth == 0) _buildObservers.RemoveAt(i);
                else _buildObservers[i].SetTarget(null!);
            }
        }
        if (_notificationDepth == 0 && _buildObservers.Count == 0) _buildObservers = null;
    }

    private void NotifyBuildObservers()
    {
        if (_buildObservers is null) return;
        _notificationDepth++;
        try
        {
            var count = _buildObservers.Count;
            for (var i = 0; i < count; i++)
                if (_buildObservers[i].TryGetTarget(out var observer)) observer.Invalidate();
        }
        finally
        {
            _notificationDepth--;
            if (_notificationDepth == 0)
            {
                for (var i = _buildObservers.Count - 1; i >= 0; i--)
                    if (!_buildObservers[i].TryGetTarget(out _)) _buildObservers.RemoveAt(i);
                if (_buildObservers.Count == 0) _buildObservers = null;
            }
        }
    }
}

// Widgets retain this small token, never an entire disposed rendering context.
internal sealed class UiFrameInvalidator
{
    internal Action? Callback;
    internal Action Request { get; }
    internal UiFrameInvalidator() => Request = Invalidate;
    private void Invalidate() => Callback?.Invoke();
}
