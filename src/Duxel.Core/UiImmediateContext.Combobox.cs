namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal bool RenderCombobox<TBehavior, TPresentation>(string id, string label, ref string text, UiOptionSelection selection,
        UiFilteredOptionSelection filtered, UiMenuInputState popup, UiMenuInputState logical, UiTextInputOwner owner,
        UiComboboxOptions options, UiBinding<bool>? controlledOpen, UiComboboxCompletion? completion = null)
        where TBehavior : IUiMenuBehavior
        where TPresentation : IUiComboboxPresentation
    {
        var initialText = text;
        var selectedBefore = selection.Count;
        filtered.EmptyText = options.EmptyText;
        var field = options.FieldOptions.WithInheritedDirection(RightToLeft);
        if (_currentField is { Invalid: true })
            field = field with
            {
                Invalid = true
            };
        var context = new UiControlContext(this);
        var size = context.ConstrainToLayout(TPresentation.Measure(context, field));
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var root = ResolveId(id);
        var editorId = ResolveCachedIdentifier(root, "##editor", false);
        var arrowName = ResolveCachedIdentifier(id, "##expand", false);
        var arrowId = ResolveId(arrowName);
        var menuId = ResolveCachedIdentifier(root, "##menu", false);
        var parent = _accessibilityParent;
        var widget = _accessibilityWidget;
        var previousBlocking = _blockingKeyboardTier;
        if (popup.Open && ReferenceEquals(_activeMenu, popup))
            _blockingKeyboardTier = _popupTierDepth;
        using var clip = context.Clip(bounds);
        using var typography = TPresentation.BeginText(context, field);
        try
        {
            var available = IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier;
            owner.Bind(this, editorId);
            if (completion is not null)
            {
                completion.EditorId = editorId;
                if (completion.FocusRequested && available)
                {
                    FocusGroupControl(editorId, keyboard: true);
                    completion.FocusRequested = false;
                }
            }
            ClaimAccessibilityField(root);
            ClaimFieldFocus(editorId, bounds);
            if (_accessibility?.Take(root, UiAccessibilityAction.Focus) is { } focus)
                ApplyAccessibilityFocus(editorId, bounds, focus);
            ApplyAccessibilityValue(root, ref text, options.MaxLength, false, bounds);
            if (_state.FocusedId == popup.Root)
                _state.FocusedId = _ownedControlFocus = editorId;
            var arrowBounds = TPresentation.ExpandBounds(bounds, field);
            var arrow = ProcessButton(arrowName, arrowBounds, TBehavior.TriggerActivation);
            arrow = ApplyAccessibilityInvoke(arrowId, arrowBounds, arrow);
            _currentTabStops.RemoveAll(stop => stop.Id == arrowId);
            if (_state.FocusedId == arrowId)
                _state.FocusedId = _ownedControlFocus = editorId;
            if (arrow.Activated)
                _state.ActiveId = editorId;
            var layout = TPresentation.EditingLayout(context, bounds, field);
            layout.Validate();
            var keys = _keyEvents;
            var characters = _charEvents;
            filtered.EditorKeys.Clear();
            filtered.EditorCharacters.Clear();
            foreach (var key in keys)
                if (key.Key is not (UiKey.UpArrow or UiKey.DownArrow)
                    && !(popup.Open && filtered.Navigated && key.Key is UiKey.Home or UiKey.End or UiKey.PageUp or UiKey.PageDown))
                    filtered.EditorKeys.Add(key);
            foreach (var character in characters)
                if (!(popup.Open && filtered.Navigated && character.CodePoint == ' '))
                    filtered.EditorCharacters.Add(character);
            var selectionBeforeEdit = _state.GetSelection(editorId);
            UiInputEditResult edit;
            try
            {
                _keyEvents = filtered.EditorKeys;
                _charEvents = filtered.EditorCharacters;
                // A menu drag owns the release; the editor must not reacquire ActiveId before the popup consumes it.
                edit = popup.Open && _state.ActiveId == popup.Root
                    ? EditSingleLineInput(editorId, ref text, options.MaxLength, layout, false, true)
                    : EditNavigableInput(editorId, ref text, options.MaxLength, layout, arrowId);
            }
            finally { _keyEvents = keys; _charEvents = characters; }
            // The editor keeps its own caret direction. When it is empty, the
            // arrow towards the preceding tokens follows the composite layout.
            var towardTokens = field.RightToLeft && text.Length == 0 ? UiKey.RightArrow : UiKey.LeftArrow;
            if (completion is not null && edit.Active && !edit.Changed && selectionBeforeEdit.Start == 0 && selectionBeforeEdit.End == 0
                && string.IsNullOrEmpty(edit.CompositionText)
                && HasTokenNavigation())
            {
                var previous = completion.PreviousGroup;
                for (var i = previous.Ids.Length - 1; i >= 0; i--)
                {
                    var index = previous.OptionIndices[i];
                    if (!selection.Selected[index] || !selection.Options[index].Enabled) continue;
                    if (popup.Open) CloseMenu(popup, restoreFocus: false);
                    FocusGroupControl(previous.Ids[i], keyboard: true);
                    edit = edit with { Active = false, FocusWithin = false };
                    break;
                }
            }
            var editedText = edit.Changed || text != initialText;
            var focusOutside = _state.FocusedId != editorId && _state.FocusedId != popup.Root && _state.FocusedId != root;
            if (!options.Freeform && (popup.DismissalVersion != filtered.HandledDismissalVersion
                || filtered.HadFocus && !edit.Active && focusOutside && !popup.Open))
                text = selection.Multiple ? string.Empty : selection.Text;
            var externalTextChanged = filtered.LastText is { } previousText && previousText != initialText;
            var textChanged = text != initialText || externalTextChanged;
            logical.Prepare(menuId, selection.MenuItems);
            var nativeSelected = ProcessOptionSelectionRequests(selection, logical, available);
            if (nativeSelected && !selection.Multiple)
            {
                text = selection.Text;
                if (popup.Open)
                    CloseMenu(popup, restoreFocus: false);
            }
            textChanged = text != initialText || externalTextChanged;
            if (textChanged)
                filtered.Navigated = false;
            var typedCommit = edit.Active && !filtered.Navigated && string.IsNullOrEmpty(edit.CompositionText)
                && keys.Any(key => key.IsDown && key.Key == UiKey.Enter && key.Modifiers == 0);
            if (typedCommit)
            {
                if (completion is null)
                    selection.CommitText(ref text, options.Freeform);
                else
                {
                    filtered.Filter(text);
                    var first = Array.FindIndex(filtered.Items, static item => item.Enabled);
                    if (first >= 0) filtered.Activate(first);
                }
                if (popup.Open && !selection.Multiple)
                    CloseMenu(popup, restoreFocus: true);
            }
            if (completion is not null && selection.Count != selectedBefore)
            {
                text = string.Empty;
                if (popup.Open) CloseMenu(popup, restoreFocus: true);
            }
            var expansionRequest = _accessibility?.Take(root, UiAccessibilityAction.Expand)
                ?? _accessibility?.Take(root, UiAccessibilityAction.Collapse);
            // Explicit expansion restores the complete collection; typed and external query edits filter it.
            if (arrow.Activated || expansionRequest is { Action: UiAccessibilityAction.Expand } && !popup.Open)
                filtered.Filter(string.Empty);
            else if (textChanged || typedCommit)
                filtered.Filter(text);
            else
                filtered.Filter(popup.IdentityItems is null ? text : filtered.CurrentFilter);
            filtered.Synchronize();
            popup.Prepare(menuId, filtered.Items);
            popup.TriggerId = root;
            popup.FocusOwnerId = editorId;
            ExposeAccessibility(root, parent, UiAccessibilityRole.ComboBox, label, bounds, text, maxLength: options.MaxLength,
                invalid: field.Invalid);
            _accessibility?.SetSelectionPolicy(root, selection.Multiple);
            _accessibilityWidget = null;
            _accessibilityParent = root;
            ExposeAccessibility(editorId, root, UiAccessibilityRole.Edit, label, layout.EditingBounds, text,
                maxLength: options.MaxLength, invalid: field.Invalid);
            ExposeAccessibility(arrowId, root, UiAccessibilityRole.Button, field.ExpandLabel, arrowBounds, focusable: false, valuePattern: false);
            var interaction = new UiButtonInteraction(arrow.Activated || !typedCommit && !nativeSelected && !popup.Open && edit.Active
                && (editedText || _leftMousePressed && Contains(layout.EditingBounds, _mousePosition)), edit.Hovered, edit.Held)
            {
                Focused = edit.Active || _state.FocusedId == editorId
            };
            var editorFocused = _state.FocusedId == editorId;
            if (popup.Open && editorFocused)
                _state.FocusedId = _ownedControlFocus = popup.Root;
            var popupKeys = _keyEvents;
            if (!string.IsNullOrEmpty(edit.CompositionText))
                _keyEvents = Array.Empty<UiKeyEvent>();
            else if (completion is not null && typedCommit)
                _keyEvents = popupKeys.Where(key => !key.IsDown || key.Key != UiKey.Enter).ToArray();
            string? invoked;
            try
            {
                var anchor = completion?.Anchor ?? bounds;
                invoked = RenderMenuPopup<TPresentation>(id, label, filtered.Items,
                    new UiMenuOptions(MathF.Max(1, anchor.Width), 32, 4, field.RightToLeft)
                    {
                        MaximumHeight = field.MaximumPopupHeight ?? MathF.Max(40, _displaySize.Y * .8f)
                    },
                    popup, anchor, interaction, filtered, controlledOpen?.Value, expansionRequest, TBehavior.OptionActivation);
            }
            finally { _keyEvents = popupKeys; }
            if (_state.FocusedId == popup.Root || _state.FocusedId == root || editorFocused && popup.Open)
                _state.FocusedId = _ownedControlFocus = editorId;
            if (completion is not null && selection.Count != selectedBefore)
            {
                text = string.Empty;
                if (popup.Open) CloseMenu(popup, restoreFocus: true);
            }
            if (invoked is not null && !selection.Multiple)
                text = selection.Text;
            if (!options.Freeform && popup.DismissalVersion != filtered.HandledDismissalVersion)
                text = selection.Multiple ? string.Empty : selection.Text;
            filtered.HandledDismissalVersion = popup.DismissalVersion;
            filtered.HadFocus = _state.ActiveId == editorId || _state.FocusedId == editorId;
            if (popup.PublishedFrame != GetFrameCount() || popup.PublishedRoot != popup.Root)
                PublishClosedOptionList(selection, logical, root, label, available);
            else if (_accessibility is { } tree)
                for (var i = 0; i < selection.Options.Length; i++)
                    if (!filtered.Contains(i))
                        tree.Add(new(logical.ItemIds[i], menuId, UiAccessibilityRole.Option,
                        selection.Options[i].Text, default, available && selection.Options[i].Enabled, true, false,
                        Focusable: false, ValuePattern: false)
                        {
                            Selected = selection.Selected[i]
                        });
            // The popup published the current projection already. Rebuild it only
            // after hidden options have been published against that same projection.
            if (completion is not null && selection.Count != selectedBefore)
                filtered.Filter(text);
            _accessibility?.SetControlledElement(root, menuId);
            _accessibility?.SetControlValue(root, text);
            edit = ReconcileInputEdit(editorId, text, edit);
            _accessibility?.SetControlValue(editorId, text, edit.AccessibilityText,
                CaptureInputTextLayout(editorId, edit.AccessibilityText, layout, edit));
            TPresentation.PaintEditor(context with { MotionKey = root }, layout, edit, field, popup.Open);
            UpdateMenuExpansion(popup);
            filtered.LastText = text;
            return text != initialText;

            bool HasTokenNavigation()
            {
                foreach (var key in keys)
                    if (key.IsDown && key.Modifiers == 0 && (key.Key == towardTokens || key.Key == UiKey.Backspace))
                        return true;
                return false;
            }
        }
        finally
        {
            _accessibilityWidget = widget;
            _accessibilityParent = parent;
            _blockingKeyboardTier = _activeMenu is { Open: true }
                ? Math.Max(previousBlocking, _blockingKeyboardTier) : _popupTierDepth;
            FinishLayoutBox(origin, size);
        }
    }
}
