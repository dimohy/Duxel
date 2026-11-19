namespace Duxel.Core;

public sealed partial class UiAccessibilityTree
{
    private List<string>? _invocationIds;
    private List<UiAccessibilityElement>? _invocations;

    internal void MarkInvoked(string id)
    {
        if (!_building || !_entries.TryGetValue(id, out var entry) || entry.Seen != _generation || !entry.Staged.SupportsInvoke)
            throw new InvalidOperationException("Invocation requires a staged action element.");
        if (AutomationEvent is null)
            return;
        var ids = _invocationIds ??= [];
        if (!ids.Contains(id))
            ids.Add(id);
    }

    private void FlushInvocations(Action<UiAccessibilityEvent>? observer)
    {
        if (_invocationIds is not { Count: > 0 } ids)
            return;
        var elements = _invocations ??= [];
        if (observer is not null)
            foreach (var id in ids)
                if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation && entry.HasPublished)
                    elements.Add(entry.Published);
        ids.Clear();
        if (observer is not null)
            NotifySurfaces(observer, elements, UiAccessibilityEventKind.Invoked);
    }

    private void ClearInvocations()
    {
        _invocationIds?.Clear();
        _invocations?.Clear();
    }
}
