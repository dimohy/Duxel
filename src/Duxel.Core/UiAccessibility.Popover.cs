namespace Duxel.Core;

public sealed partial class UiAccessibilityTree
{
    internal void RetireStagedOverlay(string id)
    {
        if (!_building)
            return;
        for (var index = _order.Count - 1; index >= 0; index--)
        {
            var entry = _entries[_order[index]];
            var current = entry;
            for (var depth = 0; depth <= _entries.Count; depth++)
            {
                if (current.Staged.Id == id)
                {
                    entry.Seen = _generation - 1;
                    _order.RemoveAt(index);
                    break;
                }
                if (current.Staged.ParentId.Length == 0 || !_entries.TryGetValue(current.Staged.ParentId, out current))
                    break;
                if (depth == _entries.Count)
                    throw new InvalidOperationException("Accessibility parent chains must be acyclic.");
            }
        }
    }

    internal void SetModal(string id, bool modal)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with
            {
                Modal = modal
            };
    }
    internal void SetOverlayFocusTarget(string id, bool focusable, bool focused)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with
            {
                Focusable = focusable,
                Focused = focused
            };
    }
    internal void SetDialogDescription(string id, string? description)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with
            {
                Hint = description
            };
    }
}
