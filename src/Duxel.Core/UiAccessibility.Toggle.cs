namespace Duxel.Core;

public enum UiAccessibilityToggleState
{
    Off,
    On,
    Indeterminate
}

public readonly record struct UiAccessibilityToggleChange
{
    public UiAccessibilityToggleChange(UiAccessibilityElement Element, bool? Previous)
    {
        this.Element = Element;
        PreviousState = UiAccessibilityToggleStates.FromChecked(Previous);
    }

    public UiAccessibilityElement Element { get; init; }
    public UiAccessibilityToggleState? PreviousState { get; init; }
    public bool? Previous
    {
        get => UiAccessibilityToggleStates.ToChecked(PreviousState);
        init => PreviousState = UiAccessibilityToggleStates.FromChecked(value);
    }

    public void Deconstruct(out UiAccessibilityElement Element, out bool? Previous)
    {
        Element = this.Element;
        Previous = this.Previous;
    }

    internal static UiAccessibilityToggleChange FromStates(UiAccessibilityElement element, UiAccessibilityToggleState? previous)
        => new(element, null) { PreviousState = previous };
}

internal static class UiAccessibilityToggleStates
{
    internal static UiAccessibilityToggleState? FromChecked(bool? value)
        => value.HasValue ? value.Value ? UiAccessibilityToggleState.On : UiAccessibilityToggleState.Off : null;

    internal static bool? ToChecked(UiAccessibilityToggleState? state) => state switch
    {
        UiAccessibilityToggleState.Off => false,
        UiAccessibilityToggleState.On => true,
        UiAccessibilityToggleState.Indeterminate or null => null,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}

public sealed partial class UiAccessibilityTree
{
    internal void UpdateToggleState(string id, bool value)
    {
        if (!TryGetStaged(id, out var node) || node.ToggleState is null)
            throw new InvalidOperationException("A deferred toggle update requires a staged toggle control.");
        _entries[id].Staged = node with { ToggleState = value ? UiAccessibilityToggleState.On : UiAccessibilityToggleState.Off };
    }
    /// <summary>Reports retained toggle state/capability changes after final publication, outside locks.</summary>
    public event Action<UiAccessibilityToggleChange>? ToggleChanged;
    private List<UiAccessibilityToggleChange>? _toggleChanges;
}
