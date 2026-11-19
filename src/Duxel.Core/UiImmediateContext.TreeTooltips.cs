namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private (UiControlTooltip? Owner, bool Captured) BeginTreeLabelTooltip(UiTreeItem item, string id)
    {
        if (item.PlainLabel?.Label.IsTruncated != true) return default;
        var owner = item.LabelTooltip ??= new UiControlTooltip
        {
            Options = new() { Relationship = UiTooltipRelationship.Label }
        };
        owner.Attach(this);
        owner.Text = item.Label;
        return (owner, BeginTooltipAccessibility(owner, owner.Text, ResolveCachedIdentifier(id, "##full-label", false)));
    }

    private void EndTreeLabelTooltip((UiControlTooltip? Owner, bool Captured) tooltip, string id, UiRect bounds)
    {
        var semantics = EndTooltipAccessibility(tooltip.Captured);
        if (tooltip.Owner is not { } owner) return;
        var visible = IntersectRect(CurrentClipRect, bounds);
        var available = visible is { Width : > 0, Height : > 0 } && _layoutPointerBlockedTier != _popupTierDepth
            && _popupTierDepth >= _blockingKeyboardTier;
        ObserveTooltipTarget<UiHoverFocusTooltipBehavior>(owner, owner.Text, owner.Options, semantics, bounds,
            available, available && !_layoutPointerBlocked && IsHovering(visible), _state.FocusedId == id, _tooltipTargetDepth + 1);
    }
}
