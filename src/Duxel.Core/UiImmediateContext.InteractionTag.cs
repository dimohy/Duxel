namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal (bool Primary, bool Dismissed) RenderInteractionTag<TPresentation>(string id, UiTagContent content, UiTagOptions options,
        bool dismissible, bool enabled, UiBinding<bool>? selected = null) where TPresentation : IUiTagPresentation
    {
        var context = new UiControlContext(this);
        var size = context.ConstrainToLayout(TPresentation.MeasureInteraction(context, content, options, dismissible));
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var edge = dismissible ? TPresentation.SecondaryWidth(options) : 0;
        if (!float.IsFinite(edge) || edge < 0 || dismissible && edge == 0)
            throw new InvalidOperationException("InteractionTag requires a finite positive secondary width.");
        edge = MathF.Min(edge, bounds.Width);
        var primaryBounds = new UiRect(bounds.X + (options.RightToLeft ? edge : 0), bounds.Y, bounds.Width - edge, bounds.Height);
        var secondaryBounds = new UiRect(options.RightToLeft ? bounds.X : bounds.X + bounds.Width - edge, bounds.Y, edge, bounds.Height);
        var root = ResolveId(id);
        var primaryName = ResolveCachedIdentifier(id, "##primary", false);
        var secondaryName = ResolveCachedIdentifier(id, "##dismiss", false);
        var parent = _accessibilityParent;
        var widget = _accessibilityWidget;
        BeginDisabled(!enabled);
        try
        {
            using var clip = context.Clip(bounds);
            var name = content.SecondaryText is null ? content.Text : content.Text + ", " + content.SecondaryText;
            ExposeAccessibility(root, parent, UiAccessibilityRole.Group, name, bounds, focusable: false, valuePattern: false, literalLabel: true);
            _accessibilityParent = root;
            _accessibilityWidget = null;
            var primaryId = ResolveId(primaryName);
            var primary = ProcessButton(primaryName, primaryBounds, UiButtonActivation.Release);
            primary = ApplyAccessibilityInvoke(primaryId, primaryBounds, primary);
            if (selected is not null)
            {
                if (ApplyAccessibilityToggle(primaryId, primaryBounds))
                    primary = primary with
                    {
                        Activated = true
                    };
                var value = selected.Value;
                if (primary.Activated)
                    selected.Value = !value;
                options = options with
                {
                    Selected = selected.Value
                };
            }
            ExposeAccessibility(primaryId, root, UiAccessibilityRole.Button, name, primaryBounds, valuePattern: false,
                toggleState: selected is null ? null : options.Selected ? UiAccessibilityToggleState.On : UiAccessibilityToggleState.Off,
                literalLabel: true);
            UiButtonInteraction secondary = default;
            if (dismissible)
            {
                var secondaryId = ResolveId(secondaryName);
                secondary = ProcessButton(secondaryName, secondaryBounds, UiButtonActivation.Release);
                secondary = ApplyAccessibilityInvoke(secondaryId, secondaryBounds, secondary);
                if (IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier && _state.FocusedId == secondaryId
                    && _keyEvents.Any(key => key.IsDown && key.Modifiers == 0 && key.Key is UiKey.Delete or UiKey.Backspace))
                    secondary = secondary with
                    {
                        Activated = true
                    };
                ExposeAccessibility(secondaryId, root, UiAccessibilityRole.Button, options.DismissLabel + " " + name, secondaryBounds,
                    valuePattern: false, literalLabel: true);
                _accessibility?.ConnectActionLabel(secondaryId, primaryId);
            }
            TPresentation.PaintInteraction(context, content, bounds, primaryBounds, secondaryBounds, options, primary, secondary, dismissible);
            return (primary.Activated, secondary.Activated);
        }
        finally
        {
            _accessibilityParent = parent;
            _accessibilityWidget = widget;
            EndDisabled();
            FinishLayoutBox(origin, size);
        }
    }
}
