namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private Dictionary<UiLabel, string>? _accessibilityLabels;
    private List<(UiLabel Label, UiWidget Target, string Id)>? _accessibilityLabelTargets;

    private void ExposeAccessibilityLabel(UiLabel label, UiRect bounds, bool enabled)
    {
        if (_accessibility is not { } tree)
            return;
        var id = ResolveId("\u001flabel-text");
        var clip = IntersectRect(CurrentClipRect, bounds);
        tree.Add(new(id, _accessibilityParent, UiAccessibilityRole.Label, label.Text, clip, enabled,
            clip.Width <= 0 || clip.Height <= 0, false, Focusable: false, ValuePattern: false));
        (_accessibilityLabels ??= []).Add(label, id);
    }

    private void ConnectAccessibilityLabels()
    {
        if (_accessibility is not { } tree || _accessibilityLabels is not { } labels
            || _accessibilityLabelTargets is not { } targets)
            return;
        foreach (var (label, target, id) in targets)
        {
            if (ReferenceEquals(label.Target, target) && labels.TryGetValue(label, out var labelId))
                tree.ConnectLabel(id, labelId);
        }
    }

    private void RetireOverlayAccessibility(string surfaceId)
    {
        if (_accessibility is not { } tree)
            return;
        tree.RetireStagedOverlay(surfaceId);
        // Retire deferred relationships in the same transaction as their overlay nodes.
        if (_accessibilityLabels is { } labels)
            foreach (var pair in labels)
                if (!tree.TryGetStaged(pair.Value, out _))
                    labels.Remove(pair.Key);
        if (_accessibilityLabelTargets is { } targets)
            for (var index = targets.Count - 1; index >= 0; index--)
                if (!tree.TryGetStaged(targets[index].Id, out _))
                    targets.RemoveAt(index);
    }

    internal void ResetLabelAccessibility()
    {
        // References live only during capture; retain bounded empty collection capacity between frames.
        if (_accessibilityLabels?.Count > 256)
            _accessibilityLabels = null;
        else
            _accessibilityLabels?.Clear();
        if (_accessibilityLabelTargets?.Capacity > 256)
            _accessibilityLabelTargets = null;
        else
            _accessibilityLabelTargets?.Clear();
    }
}
