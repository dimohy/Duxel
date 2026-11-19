namespace Duxel.Core;

/// <summary>A single borrowed action on a destination, independent of navigation selection.</summary>
public sealed record UiNavSecondaryAction(string Label, Action Invoke, UiMenuIcon Icon, bool Enabled = true)
{
    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Label);
        ArgumentNullException.ThrowIfNull(Invoke);
        if (Icon.IsEmpty) throw new ArgumentException("A navigation secondary action requires an explicit icon.", nameof(Icon));
    }
}

internal sealed class UiNavSecondaryEntry(UiNavSecondaryAction action) : IDisposable
{
    internal readonly UiNavSecondaryAction Action = action;
    internal readonly UiMenuOption[] MenuItems = [new("action", action.Label, action.Enabled) { Icon = action.Icon }];
    internal readonly UiControlTooltip Tooltip = new() { Text = action.Label };
    internal string Id = string.Empty;
    internal string Name = string.Empty;
    internal UiButtonInteraction Interaction;
    public void Dispose() => Tooltip.Dispose();
}

/// <summary>Owns one contextual menu and stable action targets. Closing or moving contexts releases popup ownership.</summary>
internal sealed class UiNavSecondaryState : IDisposable
{
    internal readonly UiOwnedMenuState Owner = new();
    internal UiNavSecondaryEntry?[][] Entries = [];
    internal UiNavSecondaryEntry? MenuEntry;
    internal string? ContextPress;
    internal int LastFrame = int.MinValue;
    internal bool OpenRequested;
    internal UiAccessibilityTree.Request? ExpansionRequest;
    internal UiVector2? PointerOffset;
    internal string? ReturnFocus;
    internal void Initialize(UiNavGroup[] groups)
    {
        Entries = groups.Select(group => group.Items.ToArray().Select(item => item.SecondaryAction is { } action
            ? new UiNavSecondaryEntry(action) : null).ToArray()).ToArray();
    }
    internal void Attach(UiImmediateContext ui)
    {
        if (Owner.Attach(ui))
        {
            ContextPress = null;
            LastFrame = int.MinValue;
            MenuEntry = null;
        }
        foreach (var group in Entries)
            foreach (var entry in group)
                if (entry is not null)
                {
                    entry.Interaction = default;
                    entry.Tooltip.Attach(ui);
                }
        OpenRequested = false;
        ExpansionRequest = null;
    }
    internal bool IsItemFocused(int group, int item, string? focused) => Entries[group][item] is { } entry && entry.Id == focused;
    public void Dispose()
    {
        Owner.Dispose();
        foreach (var group in Entries)
            foreach (var entry in group) entry?.Dispose();
        Entries = [];
        MenuEntry = null;
        ContextPress = ReturnFocus = null;
        ExpansionRequest = null;
    }
}
