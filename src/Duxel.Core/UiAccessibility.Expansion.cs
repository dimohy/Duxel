namespace Duxel.Core;

public enum UiAccessibilityExpansionState { Collapsed, Expanded, PartiallyExpanded, LeafNode }
public readonly record struct UiAccessibilityExpansionChange(UiAccessibilityElement Element, UiAccessibilityExpansionState? Previous);

public sealed partial class UiAccessibilityTree
{
    /// <summary>Reports retained expander state/capability changes after final publication, outside locks.</summary>
    public event Action<UiAccessibilityExpansionChange>? ExpansionChanged;
    private List<UiAccessibilityExpansionChange>? _expansionChanges;
    private List<UiAccessibilityElement>? _closingMenus, _openingMenus;

    internal void SetExpansion(string id, UiAccessibilityExpansionState state)
    {
        if (_entries.TryGetValue(id, out var entry) && entry.Seen == _generation)
            entry.Staged = entry.Staged with { Expansion = state };
    }

    private static bool ShownSurface(in UiAccessibilityNode node, UiAccessibilityRole role) => node.Role == role && !node.Offscreen;
    private void CollectSurfaceChanges(UiAccessibilityRole role, ref List<UiAccessibilityElement>? closing,
        ref List<UiAccessibilityElement>? opening)
    {
        foreach (var id in _removed)
        {
            var entry = _entries[id];
            if (entry.HasPublished && ShownSurface(entry.Published.Node, role)) (closing ??= []).Add(entry.Published);
        }
        foreach (var entry in _updates)
        {
            var wasShown = entry.HasPublished && ShownSurface(entry.Published.Node, role);
            var shown = ShownSurface(entry.Candidate.Node, role);
            if (wasShown && !shown) (closing ??= []).Add(entry.Published);
            if (!wasShown && shown) (opening ??= []).Add(entry.Candidate);
        }
    }
    private void CollectPublishedSurfaces(UiAccessibilityRole role, ref List<UiAccessibilityElement>? closing)
    {
        foreach (var entry in _entries.Values)
            if (entry.HasPublished && ShownSurface(entry.Published.Node, role)) (closing ??= []).Add(entry.Published);
    }
}
