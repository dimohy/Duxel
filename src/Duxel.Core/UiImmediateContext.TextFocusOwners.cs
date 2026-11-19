namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private Dictionary<string, WeakReference<UiTextInputOwner>>? _textFocusOwners;
    private int _textFocusPruneFrame = int.MinValue;
    internal int TextFocusOwnerCount => _textFocusOwners?.Count ?? 0;

    internal void RegisterTextFocusOwner(string id, UiTextInputOwner owner)
    {
        if (_textFocusOwners is null)
        {
            _textFocusOwners = new(StringComparer.Ordinal);
            _state.ControlFocusChanged += OnOwnedTextFocusChanged;
        }
        if (_textFocusPruneFrame != _state.FrameCount)
        {
            _textFocusPruneFrame = _state.FrameCount;
            foreach (var pair in _textFocusOwners)
                if (!pair.Value.TryGetTarget(out _))
                    _textFocusOwners.Remove(pair.Key);
        }
        if (_textFocusOwners.TryGetValue(id, out var existing))
            existing.SetTarget(owner);
        else
            _textFocusOwners.Add(id, new(owner));
    }

    internal void UnregisterTextFocusOwner(string id, UiTextInputOwner owner)
    {
        if (_textFocusOwners is null || !_textFocusOwners.TryGetValue(id, out var current)
            || current.TryGetTarget(out var target) && !ReferenceEquals(target, owner))
            return;
        _textFocusOwners.Remove(id);
        if (_textFocusOwners.Count != 0)
            return;
        ReleaseTextFocusOwners();
    }

    private void OnOwnedTextFocusChanged(string? previous, string? current)
    {
        if (previous is null || _textFocusOwners is null || !_textFocusOwners.TryGetValue(previous, out var weak))
            return;
        if (weak.TryGetTarget(out var owner))
            owner.FocusLost?.Invoke(this, previous, current);
        else
            _textFocusOwners.Remove(previous);
    }

    private void ReleaseTextFocusOwners()
    {
        _state.ControlFocusChanged -= OnOwnedTextFocusChanged;
        _textFocusOwners = null;
        _textFocusPruneFrame = int.MinValue;
    }

    internal void ReconcileNumericFocusCommit(string id, string previous, string text, UiAccessibilityRangeInfo? range, bool invalid)
    {
        _state.GetEditBuffer(id, previous).Sync(text);
        _state.GetHistory(id, previous).Push(text);
        _state.SetSelection(id, new(text.Length, text.Length));
        _state.SetCursor(id, text.Length);
        _accessibility?.UpdateNumericInputValue(id, text, range, invalid);
        _requestFrame?.Invoke();
    }
}

public sealed partial class UiAccessibilityTree
{
    internal void UpdateNumericInputValue(string id, string text, UiAccessibilityRangeInfo? range, bool invalid)
    {
        range?.Validate();
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
        {
            var previous = entry.Staged.TextInfo;
            var content = previous?.Content.Text == text ? previous.Content : previous is null ? null : new UiAccessibilityTextContent(text);
            entry.Staged = entry.Staged with
            {
                Value = text,
                Range = range,
                Invalid = invalid,
                TextInfo = content is null ? null : new(content, text.Length, text.Length, previous!.CanSetSelection, previous.CanEditText),
                TextLayout = ReferenceEquals(content, entry.Staged.TextLayout?.Content) ? entry.Staged.TextLayout : null,
                TextAppearance = ReferenceEquals(content, entry.Staged.TextAppearance?.Content) ? entry.Staged.TextAppearance : null
            };
        }
    }
}
