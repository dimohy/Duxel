namespace Duxel.Core;

public enum UiAccessibilityRelationshipProperty
{
    LabeledBy, ControllerFor, ContainingGrid
}

/// <summary>Immutable target-first ancestry from one committed or prepared tree.</summary>
public sealed class UiAccessibilityRelationshipTarget
{
    private readonly UiAccessibilityElement[] _ancestry;
    internal UiAccessibilityRelationshipTarget(UiAccessibilityElement[] ancestry) => _ancestry = ancestry;
    public UiAccessibilityElement Element => _ancestry[0];
    public ReadOnlySpan<UiAccessibilityElement> Ancestry => _ancestry;
}

public readonly record struct UiAccessibilityRelationshipChange(UiAccessibilityRelationshipProperty Property,
    UiAccessibilityElement Element, UiAccessibilityRelationshipTarget? Previous, UiAccessibilityRelationshipTarget? Current);

public sealed partial class UiAccessibilityTree
{
    internal void ConnectLabel(string controlId, string labelId)
        => ConnectLabeledBy(controlId, labelId, UiAccessibilityRole.Label);
    internal void ConnectActionLabel(string controlId, string labelId)
        => ConnectLabeledBy(controlId, labelId, UiAccessibilityRole.Button);
    private void ConnectLabeledBy(string controlId, string labelId, UiAccessibilityRole labelRole)
    {
        if (!TryGetStaged(controlId, out var control) || !TryGetStaged(labelId, out var label)
            || label.Role != labelRole || controlId == labelId)
            throw new InvalidOperationException($"Label relationships require both distinct controls in the current frame. Control : {controlId}; label : {labelId}; expected label role : {labelRole}.");
        _entries[controlId].Staged = control with
        {
            LabeledBy = labelId
        };
    }
    /// <summary>Runs after publication, outside reader locks; removed sources do not emit property changes.</summary>
    public event Action<UiAccessibilityRelationshipChange>? RelationshipChanged;
    private List<UiAccessibilityRelationshipChange>? _relationshipChanges;

    private void ValidateRelationship(string? id)
    {
        if (id is not null && (!_entries.TryGetValue(id, out var target) || target.Seen != _generation))
            throw new InvalidOperationException("An accessibility relationship must refer to a control in the current frame : " + id);
    }

    private void CollectRelationshipChanges()
    {
        foreach (var entry in _updates)
        {
            if (!entry.HasPublished)
                continue;
            var previous = entry.Published.Node;
            var current = entry.Candidate.Node;
            Collect(UiAccessibilityRelationshipProperty.LabeledBy, previous.LabeledBy, current.LabeledBy, entry.Candidate);
            Collect(UiAccessibilityRelationshipProperty.ControllerFor, previous.ControlledElementId, current.ControlledElementId, entry.Candidate);
            Collect(UiAccessibilityRelationshipProperty.ContainingGrid, previous.GridItem?.GridId, current.GridItem?.GridId, entry.Candidate);
        }
    }
    private void Collect(UiAccessibilityRelationshipProperty property, string? previous, string? current, UiAccessibilityElement source)
    {
        if (previous == current)
            return;
        (_relationshipChanges ??= []).Add(new(property, source,
            CaptureRelationshipTarget(previous, published: true), CaptureRelationshipTarget(current, published: false)));
    }
    private UiAccessibilityRelationshipTarget? CaptureRelationshipTarget(string? id, bool published)
    {
        if (id is null)
            return null;
        var ancestry = new List<UiAccessibilityElement>();
        while (id.Length != 0)
        {
            if (ancestry.Count >= _entries.Count)
                throw new InvalidOperationException("Accessibility relationship ancestry cannot contain a parent cycle.");
            var entry = _entries[id];
            var element = published ? entry.Published : entry.Candidate;
            ancestry.Add(element);
            id = element.Node.ParentId;
        }
        return new(ancestry.ToArray());
    }
}
