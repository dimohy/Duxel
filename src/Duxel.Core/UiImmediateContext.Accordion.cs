using System.Collections.Immutable;

namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderAccordion<TBehavior, TPresentation>(UiAccordion<TBehavior, TPresentation> accordion)
        where TBehavior : IUiButtonBehavior
        where TPresentation : IUiAccordionPresentation
    {
        EnsureNotMeasuring();
        var context = new UiControlContext(this);
        var open = accordion.ReadSelection();
        accordion.PrepareLayout(context, open);
        var origin = GetCursorScreenPos();
        var tabStart = _currentTabStops.Count;
        var wasFocusedSeen = _focusedControlSeen;
        var blockBodyPointer = false;
        var items = accordion.Items;
        // Resolve header events once before arranging the final panels. Tab order is rebuilt with panel contents below.
        for (var i = 0; i < items.Length; i++)
        {
            var suffix = accordion.Suffixes[i];
            var id = accordion.HeaderIds[i] = ResolveId(suffix);
            accordion.PanelIds[i] = id + "##panel";
            var bounds = OffsetFieldRect(accordion.HeaderBounds[i], origin);
            BeginDisabled(!items[i].Enabled);
            try
            {
                var interaction = TBehavior.Process(context, suffix, bounds);
                interaction = ApplyAccessibilityInvoke(id, bounds, interaction);
                var expansion = _accessibility?.Take(id, UiAccessibilityAction.Expand)
                    ?? _accessibility?.Take(id, UiAccessibilityAction.Collapse);
                if (expansion is { } request)
                {
                    var clipped = IntersectRect(CurrentClipRect, bounds);
                    request.Result = !IsControlEnabled ? UiAccessibilityResult.Disabled
                        : clipped.Width <= 0 || clipped.Height <= 0 || _popupTierDepth < _blockingKeyboardTier
                            ? UiAccessibilityResult.Unavailable
                            : accordion.SetExpanded(i, request.Action == UiAccessibilityAction.Expand)
                                ? UiAccessibilityResult.Success : UiAccessibilityResult.InvalidOperation;
                }
                else if (interaction.Activated && IsControlEnabled)
                    accordion.SetExpanded(i, !accordion.ReadSelection().Contains(items[i].Value, StringComparer.Ordinal));
                accordion.Interactions[i] = interaction;
                blockBodyPointer |= interaction.Hovered && _leftMousePressed;
            }
            finally { EndDisabled(); }
        }
        _currentTabStops.RemoveRange(tabStart, _currentTabStops.Count - tabStart);
        _focusedControlSeen = wasFocusedSeen;
        var finalOpen = accordion.ReadSelection();
        if (accordion.FocusedPanel >= 0 && _state.FocusedId == accordion.FocusedPanelControl
            && !finalOpen.Contains(items[accordion.FocusedPanel].Value, StringComparer.Ordinal))
        {
            _state.FocusedId = _ownedControlFocus = accordion.HeaderIds[accordion.FocusedPanel];
            _state.ActiveId = null;
            _imeHandler?.SetCompositionOwner(null);
            _requestFrame?.Invoke();
        }
        accordion.FocusedPanel = -1; accordion.FocusedPanelControl = null;
        var size = ConstrainToLayout(accordion.PrepareLayout(context, finalOpen));
        origin = AllocateLayoutBox(size);
        var root = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var previousParent = _accessibilityParent;
        var rootId = ResolveId("##accordion");
        var rootClip = IntersectRect(CurrentClipRect, root);
        using var rootClipping = context.Clip(root);
        _accessibility?.Add(new(rootId, previousParent, UiAccessibilityRole.Accordion,
            _accessibilityWidget?.AccessibleName ?? accordion.Label, rootClip, IsControlEnabled,
            rootClip.Width <= 0 || rootClip.Height <= 0, false, Focusable : false, ValuePattern : false));
        try
        {
            for (var i = 0; i < items.Length; i++)
            {
                var bounds = OffsetFieldRect(accordion.HeaderBounds[i], origin);
                var clipped = IntersectRect(rootClip, bounds);
                var expanded = finalOpen.Contains(items[i].Value, StringComparer.Ordinal);
                var enabled = IsControlEnabled && items[i].Enabled && _popupTierDepth >= _blockingKeyboardTier;
                var id = accordion.HeaderIds[i];
                if (enabled && clipped.Width > 0 && clipped.Height > 0)
                {
                    _currentTabStops.Add(new(id, _popupTierDepth));
                    if (_state.FocusedId == id) { _focusedControlSeen = true; _focusedControlTier = _popupTierDepth; }
                }
                var interaction = accordion.Interactions[i] with { Focused = _state.FocusedId == id };
                TPresentation.PaintHeader(context with { MotionKey = id }, items[i], accordion.Headers[i], bounds, expanded, interaction, accordion.Options);
                _accessibility?.Add(new(id, rootId, UiAccessibilityRole.Button, items[i].Label, clipped, enabled,
                    clipped.Width <= 0 || clipped.Height <= 0, interaction.Focused, ValuePattern : false)
                    { Expansion = expanded ? UiAccessibilityExpansionState.Expanded : UiAccessibilityExpansionState.Collapsed,
                        ControlledElementId = expanded ? accordion.PanelIds[i] : null }, bounds);
                var body = OffsetFieldRect(accordion.PanelBounds[i], origin);
                var motions = accordion.PanelMotions;
                var progress = motions?[i].Progress ?? (expanded ? 1 : 0);
                var panelClip = IntersectRect(rootClip, body with { Height = body.Height * progress });
                if (!expanded)
                {
                    if (progress > 0 && motions?[i].Paint is { } previousPaint && panelClip.Width > 0 && panelClip.Height > 0)
                        RenderPresenceChild(items[i].Content, previousPaint, body, panelClip, live : false, opacity : progress);
                    continue;
                }
                var panelId = accordion.PanelIds[i];
                _accessibility?.Add(new(panelId, rootId, UiAccessibilityRole.AccordionPanel, items[i].Label, panelClip,
                    IsControlEnabled, panelClip.Width <= 0 || panelClip.Height <= 0, false, Focusable : false, ValuePattern : false)
                    { LabeledBy = id }, body);
                if (progress <= 0)
                    continue;
                _accessibilityParent = panelId;
                var panelTabStart = _currentTabStops.Count;
                if (motions is not null && panelClip.Width > 0 && panelClip.Height > 0)
                    RenderPresenceChild(items[i].Content, motions[i].Paint ??= new(), body, panelClip, live : true,
                        opacity : progress, blockPointer : blockBodyPointer);
                else
                    RenderParentDataChild(items[i].Content, null, body, motions is null ? root : panelClip, blockBodyPointer);
                for (var tab = panelTabStart; tab < _currentTabStops.Count; tab++)
                    if (_currentTabStops[tab].Id == _state.FocusedId)
                    {
                        accordion.FocusedPanel = i;
                        accordion.FocusedPanelControl = _state.FocusedId;
                    }
                _accessibilityParent = previousParent;
            }
        }
        finally { _accessibilityParent = previousParent; }
        if (accordion.NextPanelFrame is { } next)
            RequestFrameAt(next);
        FinishLayoutBox(origin, size);
    }

}
