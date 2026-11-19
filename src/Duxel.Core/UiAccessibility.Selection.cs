namespace Duxel.Core;

public sealed partial class UiAccessibilityTree
{
    // The caller holds the publication read lock while validating an action.
    private bool HasOtherSelectedChild(UiAccessibilityElement parent, string exceptId)
    {
        if (UiAccessibilitySelectionProjection.Capture(_published, parent.Node.Id, parent.RuntimeId, out var selection)
            == UiAccessibilityResult.Success)
            return selection!.SelectedItems.Any(child => child.Node.Id != exceptId);
        return false;
    }
}
