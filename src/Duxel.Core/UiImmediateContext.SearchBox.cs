namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderSearchBox<TPresentation>(string id, string label, UiBinding<string> binding, int maxLength,
        UiTextInputOwner owner, UiSearchBoxOptions options, UiButtonActivation clearPointerActivation)
        where TPresentation : IUiSearchBoxPresentation
    {
        var text = UiSearchBox<TPresentation>.ValidateText(binding.Value, maxLength);
        var initial = text;
        if (_currentField is { Invalid: true })
            options = options with
            {
                Invalid = true
            };
        var context = new UiControlContext(this);
        var size = context.ConstrainToLayout(TPresentation.Measure(context, options));
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y,
            TPresentation.EditorRole == UiAccessibilityRole.SearchBox ? MathF.Min(468, size.X) : size.X, size.Y);
        var root = ResolveId(id);
        var clearName = ResolveCachedIdentifier(id, "##clear", false);
        var clearId = ResolveId(clearName);
        using var clip = context.Clip(bounds);
        using var typography = TPresentation.BeginText(context, options);
        try
        {
            owner.Bind(this, root);
            ClaimAccessibilityField(root);
            ClaimFieldFocus(root, bounds);
            var focused = _state.FocusedId == root || _state.ActiveId == root || _state.FocusedId == clearId;
            bool ClearSlot(bool hasFocus, string value) => hasFocus || TPresentation.KeepClearForText && value.Length != 0;
            var clearSlot = ClearSlot(focused, text);
            var showClear = !options.ReadOnly && clearSlot;
            var layout = TPresentation.Arrange(context, bounds, options, clearSlot);
            layout.Validate();
            var clear = default(UiButtonInteraction);
            if (showClear && layout.After is { } clearBounds)
            {
                clear = ProcessButton(clearName, clearBounds, clearPointerActivation);
                clear = ApplyAccessibilityInvoke(clearId, clearBounds, clear);
                _currentTabStops.RemoveAll(stop => stop.Id == clearId);
                if (_state.FocusedId == clearId || clear.Activated)
                {
                    _state.FocusedId = _ownedControlFocus = root;
                    // The clear button retains the opening press until release.
                    if (clear.Activated && !(clearPointerActivation == UiButtonActivation.Press
                        && _leftMouseDown && _state.ActiveId == clearId))
                        _state.ActiveId = root;
                }
            }
            var clearOwnsPress = clear.Activated && clearPointerActivation == UiButtonActivation.Press
                && clear.Hovered && _leftMousePressed;
            var edit = EditNavigableInput(root, ref text, maxLength, layout, clearId, pointerPressConsumed: clearOwnsPress);
            if (clear.Activated)
                text = string.Empty;
            if (text != initial)
            {
                binding.Value = text;
                text = UiSearchBox<TPresentation>.ValidateText(binding.Value, maxLength);
            }
            edit = ReconcileInputEdit(root, text, edit, preserveHistory: clear.Activated && text != initial) with
            {
                FocusWithin = _state.FocusedId == root
            };
            focused = edit.Active || _state.FocusedId == root;
            clearSlot = ClearSlot(focused, text);
            showClear = !options.ReadOnly && clearSlot;
            var finalLayout = TPresentation.Arrange(context, bounds, options, clearSlot);
            finalLayout.Validate();
            TPresentation.Paint(context, finalLayout, edit, options, clear, showClear);
            ExposeAccessibility(root, _accessibilityParent, TPresentation.EditorRole, label, bounds, text,
                options.ReadOnly, maxLength, invalid: options.Invalid, textInfo: edit.AccessibilityText,
                textLayout: CaptureInputTextLayout(root, edit.AccessibilityText, finalLayout, edit));
            if (showClear && finalLayout.After is { } finalClear)
            {
                var widget = _accessibilityWidget;
                try
                {
                    _accessibilityWidget = null;
                    ExposeAccessibility(clearId, root, UiAccessibilityRole.Button, options.ClearLabel, finalClear,
                        focusable: false, valuePattern: false);
                }
                finally { _accessibilityWidget = widget; }
            }
        }
        finally { FinishLayoutBox(origin, size); }
    }

    private UiInputEditResult ReconcileInputEdit(string id, string text, UiInputEditResult edit, bool preserveHistory = false)
    {
        if (text == edit.Text)
            return edit;
        // An accepted auxiliary edit must not be mistaken for an external model replacement.
        var previous = preserveHistory ? edit.Text : text;
        _state.GetEditBuffer(id, previous).Sync(text);
        _state.GetHistory(id, previous).Push(text);
        _state.SetSelection(id, new(text.Length, text.Length));
        _state.SetCursor(id, text.Length);
        return edit with
        {
            Text = text,
            CaretIndex = text.Length,
            Selection = new(text.Length, text.Length),
            ScrollX = 0,
            AccessibilityText = CaptureAccessibilityText(id, text, text.Length, text.Length)
        };
    }
}
