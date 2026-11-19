namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool RadioGroup<TBehavior, TPresentation>(string label, UiRadioGroupContent<TPresentation> content, ref string? value, float width = 0)
        where TBehavior : IUiRadioGroupBehavior where TPresentation : IUiRadioGroupPresentation =>
        RadioGroup<TBehavior, TPresentation>(label, label, content, ref value, width);

    public bool RadioGroup<TBehavior, TPresentation>(string label, UiRadioGroupContent<TPresentation> content, ref string? value, float width, bool constrainLayout)
        where TBehavior : IUiRadioGroupBehavior where TPresentation : IUiRadioGroupPresentation =>
        RadioGroup<TBehavior, TPresentation>(label, label, content, ref value, width, constrainLayout);

    internal bool RadioGroup<TBehavior, TPresentation>(string id, string label, UiRadioGroupContent<TPresentation> content, ref string? value, float width, bool constrainLayout = false)
        where TBehavior : IUiRadioGroupBehavior where TPresentation : IUiRadioGroupPresentation
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!float.IsFinite(width) || width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        var items = content.Items;
        var context = new UiControlContext(this);
        using var typography = TPresentation.BeginText(context);
        var key = CurrentControlTextLayoutKey;
        var cacheable = CanRetainControlTextLayout;
        var measuredWidth = width > 0 ? width : MathF.Max(1, GetContentRegionAvail().X);
        if (constrainLayout)
            measuredWidth = ConstrainLayoutWidth(measuredWidth);
        content.Measure(this, context, GetDisplayLabel(label), measuredWidth, key, cacheable);
        try
        {
            var layoutRoot = content.ResolveLayout(context, constrainLayout);
            var position = AdvanceCursor(layoutRoot.Size);
            var bounds = new UiRect(position.X, position.Y, layoutRoot.Size.X, layoutRoot.Size.Y);
            var interaction = TBehavior.Process(context, id, bounds, items, content.Layouts, value, content.Input);
            interaction = ApplyRadioAccessibility(id, label, bounds, content, interaction);
            value = content.Input.Value;
            TPresentation.PaintLabel(context, layoutRoot, bounds);
            for (var i = 0; i < items.Length; i++)
            {
                var layout = content.Layouts[i];
                layout = layout with
                {
                    Bounds = layout.Bounds with
                    {
                        X = layout.Bounds.X + bounds.X,
                        Y = layout.Bounds.Y + bounds.Y
                    }
                };
                BeginDisabled(!items[i].Enabled);
                try
                {
                    TPresentation.PaintOption(context, layout, string.Equals(items[i].Value, value, StringComparison.Ordinal),
                        new(false, i == interaction.HoveredIndex, i == interaction.HeldIndex)
                        {
                            Focused = interaction.Control.Focused && i == interaction.FocusedIndex,
                            FocusVisible = interaction.Control.FocusVisible && i == interaction.FocusedIndex
                        });
                }
                finally { EndDisabled(); }
            }
            return content.Input.Changes.Length != 0;
        }
        finally { content.EndFrame(); }
    }

    internal UiRadioGroupInteraction ProcessRadioGroup(string id, UiRect bounds, ReadOnlySpan<UiRadioOption> items,
        ReadOnlySpan<UiRadioOptionLayout> layouts, string? selected, UiRadioGroupInputState state, UiButtonActivation activation, bool rightToLeft = false, bool suppressPointerActivation = false)
    {
        EnsureNotMeasuring();
        state.Begin(selected);
        var spaceActive = _spacePressOwner == ResolveId(id) && !_spaceCanceled;
        var first = -1;
        for (var i = 0; i < items.Length; i++)
            if (items[i].Enabled)
            {
                first = i;
                break;
            }
        BeginDisabled(first < 0);
        UiButtonInteraction control;
        try
        {
            control = ProcessButton<SpaceControlKeys>(id, bounds, activation);
        }
        finally { EndDisabled(); }
        if (!IsControlEnabled || first < 0)
        {
            state.PressedValue = null;
            return new(control, -1, -1, -1);
        }
        var focused = first;
        var hit = -1;
        for (var i = 0; i < items.Length; i++)
        {
            if (!items[i].Enabled)
                continue;
            if (string.Equals(items[i].Value, selected, StringComparison.Ordinal))
                focused = i;
            if (!control.Hovered)
                continue;
            var rect = layouts[i].Bounds;
            var clip = IntersectRect(CurrentClipRect, new(bounds.X + rect.X, bounds.Y + rect.Y, rect.Width, rect.Height));
            if (_mousePosition.X >= clip.X && _mousePosition.X < clip.X + clip.Width && _mousePosition.Y >= clip.Y && _mousePosition.Y < clip.Y + clip.Height)
                hit = i;
        }
        if (control.Focused && state.AccessibilityFocusedValue is { } accessibilityFocus)
            for (var i = 0; i < items.Length; i++)
                if (items[i].Enabled && items[i].Value == accessibilityFocus)
                {
                    focused = i;
                    break;
                }
        if (!control.Focused || _leftMousePressed)
            state.AccessibilityFocusedValue = null;
        if (_leftMousePressed)
            state.PressedValue = hit < 0 ? null : items[hit].Value;
        var pointerActivation = activation == UiButtonActivation.Press ? _leftMousePressed : _leftMouseReleased;
        if (!suppressPointerActivation && control.Activated && pointerActivation && hit >= 0 && state.PressedValue == items[hit].Value)
        {
            state.Select(items[hit].Value);
            focused = hit;
        }
        if (control.Focused)
        {
            foreach (var key in _keyEvents)
            {
                if ((key.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Super)) != 0)
                    continue;
                if (key.Key == UiKey.Escape && key.IsDown)
                {
                    spaceActive = false;
                    state.PressedValue = null;
                    if (_state.ActiveId == _lastItemId)
                        _state.ActiveId = null;
                }
                if (key.Key == UiKey.Space)
                {
                    if (key.IsDown)
                        spaceActive = !_spaceCanceled;
                    else
                    {
                        if (spaceActive && control.Activated)
                            state.Select(items[focused].Value);
                        spaceActive = false;
                    }
                }
                if (!key.IsDown)
                    continue;
                var direction = key.Key is UiKey.LeftArrow or UiKey.UpArrow ? -1 : key.Key is UiKey.RightArrow or UiKey.DownArrow ? 1 : 0;
                if (rightToLeft && key.Key is UiKey.LeftArrow or UiKey.RightArrow)
                    direction = -direction;
                if (direction == 0)
                    continue;
                do
                {
                    focused = (focused + direction + items.Length) % items.Length;
                } while (!items[focused].Enabled);
                state.Select(items[focused].Value);
            }
        }
        var held = control.Held && hit >= 0 && state.PressedValue == items[hit].Value ? hit : -1;
        if (!_leftMouseDown)
            state.PressedValue = null;
        _lastItemEdited = state.Changes.Length != 0;
        _lastItemToggledSelection = _lastItemEdited;
        if (control.Focused && state.AccessibilityFocusedValue is not null)
            state.AccessibilityFocusedValue = items[focused].Value;
        if (_lastItemEdited)
            _requestFrame?.Invoke();
        return new(control, hit, held, focused);
    }
}
