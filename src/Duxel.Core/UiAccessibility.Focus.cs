namespace Duxel.Core;

public readonly record struct UiAccessibilityFocusChange(UiAccessibilityElement? Previous, UiAccessibilityElement? Current);

public sealed partial class UiAccessibilityTree
{
    /// <summary>Reports a changed eligible focus target after publication, outside reader locks.</summary>
    public event Action<UiAccessibilityFocusChange>? FocusChanged;

    private void NotifyFocusChanged(Action<UiAccessibilityFocusChange> observer, UiAccessibilityFocusChange change)
    {
        _notifying = true;
        try { observer(change); }
        finally { _notifying = false; }
    }
}
