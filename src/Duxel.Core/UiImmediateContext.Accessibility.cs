namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private UiAccessibilityTree? _accessibility;
    /// <summary>The borrowed platform-owned tree for subscriptions and snapshots; callers must not dispose it.</summary>
    public UiAccessibilityTree? AccessibilityTree => _accessibility;
    private UiWidget? _accessibilityWidget;
    internal UiWidget? SwapAccessibilityWidget(UiWidget? widget)
    {
        if (_accessibility is null)
            return null;
        var previous = _accessibilityWidget;
        _accessibilityWidget = widget;
        return previous;
    }
    private string _accessibilityParent = string.Empty;
    private string? _accessibilityFieldTarget, _accessibilityFocus;
    private string? _accessibilityFocusOwner, _accessibilityFocusTarget;
    private readonly record struct TooltipAccessibilityScope(UiAccessibilityTooltip Info, string? Target);
    private List<TooltipAccessibilityScope>? _tooltipAccessibilityScopes;
    internal void SetAccessibilityTree(UiAccessibilityTree? tree)
    {
        UpdateClippedCodeLayouts(ReferenceEquals(_accessibility, tree));
        _accessibility = tree;
        _accessibilityWidget = null;
        _accessibilityParent = string.Empty;
        _accessibilityFieldTarget = _accessibilityFocus = _accessibilityFocusOwner = _accessibilityFocusTarget = null;
        _tooltipAccessibilityScopes?.Clear();
        ResetLabelAccessibility();
    }
    internal void EndAccessibilityFrame()
    {
        var focused = _state.FocusedId;
        if (focused is not null && focused == _accessibilityFocusOwner)
            focused = _accessibilityFocusTarget;
        try
        {
            ConnectAccessibilityLabels();
            _accessibility?.EndFrame(focused, authoritativeFocus: true);
        }
        finally { ResetLabelAccessibility(); }
    }
    private void ClaimAccessibilityField(string id)
    {
        if (_accessibility is null)
        {
            return;
        }
        if (_currentField.HasValue)
        {
            _accessibilityFieldTarget ??= id;
        }
        ClaimTooltipAccessibilityTarget(id);
    }

    private bool BeginTooltipAccessibility(IUiTooltipTarget owner, string text, string? sourceId = null)
    {
        if (_accessibility is null)
        {
            return false;
        }
        var scopes = _tooltipAccessibilityScopes ??= [];
        scopes.Add(new(new(sourceId ?? ResolveId("\u001ftooltip-description"), text, owner.Options.Relationship), null));
        return true;
    }

    private TooltipAccessibilityScope? EndTooltipAccessibility(bool captured)
    {
        if (captured)
        {
            var scopes = _tooltipAccessibilityScopes!;
            var result = scopes[^1];
            scopes.RemoveAt(scopes.Count - 1);
            return result;
        }
        return null;
    }

    private UiAccessibilityScrollInfo? ApplyAccessibilityScrollRequest(UiAccessibilityTree tree, string id, UiVector2 viewport,
        UiRect clip, UiVector2 content, float step, bool enabled, bool rightToLeft, ref UiVector2 offset)
    {
        var scroll = new UiAccessibilityScrollInfo(viewport, content, offset) { RightToLeft = rightToLeft };
        if (TakeAccessibilityScrollRequest(tree, id, clip, scroll, enabled) is { Scroll: { } command } request)
        {
            offset = command.Apply(scroll, step);
            scroll = scroll with
            {
                Offset = offset
            };
            request.Result = UiAccessibilityResult.Success;
        }
        return CanPublishScroll(clip, scroll) ? scroll : null;
    }

    private static bool CanPublishScroll(UiRect clip, in UiAccessibilityScrollInfo scroll) =>
        clip.Width > 0 && clip.Height > 0 && (scroll.HorizontallyScrollable || scroll.VerticallyScrollable);

    private static UiAccessibilityTree.Request? TakeAccessibilityScrollRequest(UiAccessibilityTree tree, string id,
        UiRect clip, in UiAccessibilityScrollInfo scroll, bool enabled)
    {
        if (tree.Take(id, UiAccessibilityAction.Scroll) is not { Scroll: { } command } request)
            return null;
        var available = clip.Width > 0 && clip.Height > 0;
        if (!enabled)
            request.Result = UiAccessibilityResult.Disabled;
        else if (!available)
            request.Result = UiAccessibilityResult.Unavailable;
        else if (!CanPublishScroll(clip, scroll) || !command.CanApply(scroll))
            request.Result = UiAccessibilityResult.InvalidOperation;
        else
            return request;
        return null;
    }

    private UiAccessibilityNode? PrepareTooltipSurface(TooltipAccessibilityScope? scope, string text, UiPopupLayout layout,
        UiTooltipOptions options, ref UiVector2 offset)
    {
        if (_accessibility is not { } tree || scope is not { Target: { } target } semantic
            || semantic.Info.Relationship == UiTooltipRelationship.Inaccessible || !tree.TryGetStaged(target, out _))
        {
            return null;
        }
        var clip = IntersectRect(_clipRect, layout.Bounds);
        var scroll = ApplyAccessibilityScrollRequest(tree, semantic.Info.SourceId, new(layout.Bounds.Width, layout.Bounds.Height), clip,
            layout.ContentSize, options.ScrollStep ?? 48, enabled: true, rightToLeft: options.Positioning.RightToLeft, ref offset);
        return new UiAccessibilityNode(semantic.Info.SourceId, target, UiAccessibilityRole.Tooltip, text, clip, true,
            clip.Width <= 0 || clip.Height <= 0, false, Focusable: false, ValuePattern: false)
        {
            Scroll = scroll
        };
    }

    private void ClaimTooltipAccessibilityTarget(string id)
    {
        if (_tooltipAccessibilityScopes is not { Count: > 0 } scopes)
        {
            return;
        }
        // Reserve the primary control before its adornments render. An inner scope owns its relationship.
        for (var i = 0; i < scopes.Count; i++)
        {
            if (scopes[i].Target is null)
            {
                scopes[i] = scopes[i] with
                {
                    Target = id
                };
            }
        }
    }

    private UiAccessibilityTooltip? TooltipAccessibility(string id)
    {
        if (_tooltipAccessibilityScopes is not { Count: > 0 } scopes)
        {
            return null;
        }
        var current = scopes[^1];
        return current.Target == id && current.Info.Relationship != UiTooltipRelationship.Inaccessible
            ? current.Info : null;
    }
    private void ExposeAccessibility(string id, string parent, UiAccessibilityRole role, string label, UiRect bounds,
        string? value = null, bool readOnly = false, int maxLength = 0, bool focusable = true,
        bool valuePattern = true, bool multiline = false, bool invalid = false, UiAccessibilityScrollInfo? scroll = null,
        bool virtualItemContainer = false, UiAccessibilityRangeInfo? range = null, UiAccessibilityToggleState? toggleState = null,
        bool selectionRequired = false, bool literalLabel = false, UiVector2? clickablePoint = null, UiAccessibilityTextInfo? textInfo = null,
        UiAccessibilityTextLayout? textLayout = null, UiAccessibilityImageInfo? imageInfo = null, bool busy = false, string? hint = null)
    {
        if (_accessibility is not { } tree)
            return;
        var field = _accessibilityFieldTarget == id ? _currentField : null;
        ClaimTooltipAccessibilityTarget(id);
        var tooltip = TooltipAccessibility(id);
        var standalone = id == ResolveId(UiControlWidget.PrimaryControlId) ? UiLabel.Find(_accessibilityWidget) : null;
        if (standalone is not null && _accessibilityWidget is { } target)
            (_accessibilityLabelTargets ??= []).Add((standalone, target, id));
        var name = _accessibilityWidget?.AccessibleName ?? field?.Label ?? standalone?.Text
            ?? (tooltip is { Relationship: UiTooltipRelationship.Label } naming ? naming.Text : literalLabel ? label : GetDisplayLabel(label));
        var clip = IntersectRect(CurrentClipRect, bounds);
        tree.Add(new(id, parent, role, name, clip, IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier,
            clip.Width <= 0 || clip.Height <= 0, focusable && _state.FocusedId == id, value, readOnly, maxLength,
            field?.Hint is { Length: > 0 } fieldHint && hint is not null ? hint + " " + fieldHint : hint ?? field?.Hint,
            field?.ValidationMessage, (field?.Required ?? false) || (standalone?.Options.Required ?? false), invalid || (field?.Invalid ?? false),
            focusable, valuePattern)
        {
            Multiline = multiline,
            Busy = busy,
            Tooltip = tooltip,
            Scroll = scroll,
            VirtualItemContainer = virtualItemContainer,
            Range = range,
            TextInfo = textInfo,
            TextLayout = textLayout,
            ImageInfo = imageInfo,
            ToggleState = toggleState,
            SelectionRequired = selectionRequired,
            ClickablePoint = clickablePoint
        },
            parent.Length == 0 ? null : bounds);
    }
    private bool ApplyAccessibilityValue(string id, ref string value, int maxLength, bool readOnly,
        UiRect bounds, bool multiline = false)
    {
        var request = TakeAccessibilityTextValue(id, maxLength, readOnly, bounds, multiline);
        if (request is null)
            return false;
        var changed = value != request.Value;
        value = request.Value!;
        request.Result = UiAccessibilityResult.Success;
        return changed;
    }
    private UiAccessibilityTree.Request? TakeAccessibilityTextValue(string id, int maxLength, bool readOnly,
        UiRect bounds, bool multiline = false)
    {
        var request = _accessibility?.Take(id, UiAccessibilityAction.SetValue);
        if (request is null)
            return null;
        if (!IsControlEnabled)
        {
            request.Result = UiAccessibilityResult.Disabled;
            return null;
        }
        var clip = IntersectRect(CurrentClipRect, bounds);
        if (clip.Width <= 0 || clip.Height <= 0 || _popupTierDepth < _blockingKeyboardTier)
        {
            request.Result = UiAccessibilityResult.Unavailable;
            return null;
        }
        if (readOnly)
        {
            request.Result = UiAccessibilityResult.ReadOnly;
            return null;
        }
        if (request.Value is not { } next || next.Length > maxLength || UiAccessibilityTree.ContainsControl(next, multiline))
        {
            request.Result = UiAccessibilityResult.InvalidArgument;
            return null;
        }
        return request;
    }
    private UiButtonInteraction ApplyAccessibilityInvoke(string id, UiRect bounds, UiButtonInteraction interaction)
    {
        var request = _accessibility?.Take(id, UiAccessibilityAction.Invoke);
        if (request is null)
            return interaction;
        var clip = IntersectRect(CurrentClipRect, bounds);
        if (!IsControlEnabled)
        {
            request.Result = UiAccessibilityResult.Disabled;
            return interaction;
        }
        if (clip.Width <= 0 || clip.Height <= 0 || _popupTierDepth < _blockingKeyboardTier)
        {
            request.Result = UiAccessibilityResult.Unavailable;
            return interaction;
        }
        request.Result = UiAccessibilityResult.Success;
        _requestFrame?.Invoke();
        return interaction with
        {
            Activated = true
        };
    }
    private void ApplyAccessibilityFocus(string id, UiRect bounds, bool allowDisabledFocus = false)
    {
        ObserveOverlayFocusReturnTarget(id, bounds, allowDisabledFocus);
        var request = _accessibility?.Take(id, UiAccessibilityAction.Focus);
        if (request is null)
            return;
        ApplyAccessibilityFocus(id, bounds, request, allowDisabledFocus);
    }
    private void ApplyAccessibilityFocus(string id, UiRect bounds, UiAccessibilityTree.Request request, bool allowDisabledFocus = false)
    {
        var clip = IntersectRect(CurrentClipRect, bounds);
        if (!IsControlEnabled && !allowDisabledFocus)
        {
            request.Result = UiAccessibilityResult.Disabled;
            return;
        }
        if (clip.Width <= 0 || clip.Height <= 0 || _popupTierDepth < _blockingKeyboardTier)
        {
            request.Result = UiAccessibilityResult.Unavailable;
            return;
        }
        if (_state.ActiveId is { } previous && _state.HasTextInputStorage(previous))
            _imeHandler?.SetCompositionOwner(null);
        _state.ActiveId = null;
        _state.PendingTextInputFocusId = null;
        _state.FocusedId = id;
        _ownedControlFocus = id;
        _state.NavCursorVisible = true;
        _accessibilityFocus = id;
        CancelControlSpace();
        request.Result = UiAccessibilityResult.Success;
        _requestFrame?.Invoke();
    }
}
