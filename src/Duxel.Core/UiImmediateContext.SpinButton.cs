namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void ReleaseSpinCapture(string? increment, string? decrement)
    {
        if (_state.ActiveId is { } active && (active == increment || active == decrement))
            _state.ActiveId = null;
    }

    internal void RenderSpinButton<TPresentation>(UiSpinButton<TPresentation> widget, string id)
        where TPresentation : IUiSpinButtonPresentation
    {
        var options = widget.Options;
        var numbers = widget.Numbers;
        var state = widget.Input;
        var model = widget.Value.Value;
        numbers.ValidateValue(model);
        state.Bind(this, _state.FrameCount);
        if (!state.Initialized || model != state.LastModel || state.DisplayDirty && !state.Editing)
        {
            state.Draft = widget.Format(model);
            state.LastModel = model;
            state.Initialized = true;
            state.Editing = state.DisplayDirty = false;
        }
        if (_currentField is { Invalid: true })
            options = options with
            {
                Invalid = true
            };
        state.BaseInvalid = options.Invalid;
        var context = new UiControlContext(this);
        var size = context.ConstrainToLayout(TPresentation.Measure(context, options));
        var origin = AllocateLayoutBox(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var root = ResolveId(id);
        var incrementName = ResolveCachedIdentifier(id, "##increment", false);
        var decrementName = ResolveCachedIdentifier(id, "##decrement", false);
        var incrementId = state.Increment = ResolveId(incrementName);
        var decrementId = state.Decrement = ResolveId(decrementName);
        using var clip = context.Clip(bounds);
        using var typography = TPresentation.BeginText(context, options);
        try
        {
            widget.Editor.Bind(this, root);
            ClaimAccessibilityField(root);
            ClaimFieldFocus(root, bounds);
            var layout = TPresentation.Arrange(context, bounds, options);
            layout.Input.Validate();
            void CommitText()
            {
                var parsed = widget.Parse(state.Draft);
                widget.Commit(state.Draft, UiSpinButtonChangeReason.Text, parsed.Value, parsed.Success);
            }
            bool Move(int direction, bool page = false)
            {
                var parsed = widget.Parse(state.Draft);
                var from = state.Editing && parsed.Success ? parsed.Value : widget.Value.Value;
                var next = numbers.Move(from, direction, page);
                var reason = page ? direction > 0 ? UiSpinButtonChangeReason.PageIncrement : UiSpinButtonChangeReason.PageDecrement
                    : direction > 0 ? UiSpinButtonChangeReason.Increment : UiSpinButtonChangeReason.Decrement;
                return widget.Commit(state.Draft, reason, next, true);
            }
            if (state.Editing && (_state.IsControlFocusLeaving(root) || _leftMousePressed && !IsHovering(bounds)))
                CommitText();
            var valueRequest = TakeAccessibilityTextValue(root, widget.MaxLength, options.ReadOnly, bounds);
            if (valueRequest is not null)
            {
                var parsed = widget.Parse(valueRequest.Value!);
                var accepted = widget.Commit(valueRequest.Value!, UiSpinButtonChangeReason.Accessibility, parsed.Value, parsed.Success);
                valueRequest.Result = accepted ? UiAccessibilityResult.Success : UiAccessibilityResult.InvalidArgument;
            }
            var rangeRequest = TakeAccessibilityRangeValue(root, bounds, numbers.Minimum, numbers.Maximum);
            if (rangeRequest is not null)
            {
                if (options.ReadOnly)
                    rangeRequest.Result = UiAccessibilityResult.ReadOnly;
                else
                {
                    var accepted = widget.Commit(state.Draft, UiSpinButtonChangeReason.Accessibility,
                        numbers.Normalize(rangeRequest.RangeValue!.Value), true);
                    rangeRequest.Result = accepted ? UiAccessibilityResult.Success : UiAccessibilityResult.InvalidArgument;
                }
            }
            var canIncrement = !options.ReadOnly && numbers.CanMove(widget.Value.Value, 1);
            var canDecrement = !options.ReadOnly && numbers.CanMove(widget.Value.Value, -1);
            UiButtonInteraction Stepper(string name, string resolved, UiRect rect, bool enabled)
            {
                BeginDisabled(!enabled);
                try
                {
                    var interaction = ProcessButton(name, rect, UiButtonActivation.Press);
                    interaction = ApplyAccessibilityInvoke(resolved, rect, interaction);
                    _currentTabStops.RemoveAll(stop => stop.Id == resolved);
                    if (_state.FocusedId == resolved || interaction.Activated)
                        _state.FocusedId = _ownedControlFocus = root;
                    return interaction;
                }
                finally { EndDisabled(); }
            }
            var increment = Stepper(incrementName, incrementId, layout.Increment, canIncrement);
            var decrement = Stepper(decrementName, decrementId, layout.Decrement, canDecrement);
            if (!_leftMouseDown || increment.Activated || decrement.Activated)
                state.CancelledHold = false;
            if (_leftMouseDown && (_state.ActiveId == incrementId && !increment.Hovered || _state.ActiveId == decrementId && !decrement.Hovered))
                state.CancelledHold = true;
            var heldDirection = state.CancelledHold ? 0 : increment.Held && increment.Hovered && canIncrement ? 1
                : decrement.Held && decrement.Hovered && canDecrement ? -1 : 0;
            var acceptedStep = true;
            if (increment.Activated || decrement.Activated)
            {
                acceptedStep = Move(increment.Activated ? 1 : -1);
                state.Stop();
            }
            if (heldDirection == 0 || !acceptedStep)
                state.Stop();
            else
            {
                var now = _state.InputTimeSeconds;
                if (state.Direction != heldDirection)
                {
                    state.Direction = heldDirection;
                    state.RepeatStarted = now;
                    state.RepeatAt = now + .3;
                }
                else if (state.RepeatAt is { } deadline && now >= deadline)
                {
                    if (Move(heldDirection))
                        state.RepeatAt = now + Math.Max(.08, .3 - .22 * Math.Min(1, now - state.RepeatStarted));
                    else
                        state.RepeatAt = null;
                }
                if (!numbers.CanMove(widget.Value.Value, heldDirection))
                    state.RepeatAt = null;
                if (state.RepeatAt is { } next)
                    RequestFrameAt(_state.TimeSeconds + Math.Max(0, next - now));
            }
            var text = state.Draft;
            var retainedPointer = _state.ActiveId == incrementId ? incrementId : _state.ActiveId == decrementId ? decrementId : null;
            var edit = EditNavigableInput(root, ref text, widget.MaxLength, layout.Input, retainedPointer);
            if (text != state.Draft)
            {
                state.Draft = text;
                state.Editing = true;
            }
            if (_state.FocusedId == root && IsControlEnabled && !options.ReadOnly && _popupTierDepth >= _blockingKeyboardTier)
            {
                foreach (var key in _keyEvents)
                {
                    if (!key.IsDown)
                        continue;
                    if (key.Key == UiKey.UpArrow)
                        Move(1);
                    else if (key.Key == UiKey.DownArrow)
                        Move(-1);
                    else if (key.Key == UiKey.PageUp)
                        Move(1, true);
                    else if (key.Key == UiKey.PageDown)
                        Move(-1, true);
                    else if (key.Key == UiKey.Home && !key.Modifiers.HasFlag(KeyModifiers.Shift) && options.Minimum is { } min)
                        widget.Commit(state.Draft, UiSpinButtonChangeReason.Minimum, min, true);
                    else if (key.Key == UiKey.End && !key.Modifiers.HasFlag(KeyModifiers.Shift) && options.Maximum is { } max)
                        widget.Commit(state.Draft, UiSpinButtonChangeReason.Maximum, max, true);
                    else if (key.Key == UiKey.Enter)
                        CommitText();
                    else if (key.Key == UiKey.Escape && !_tooltipEscapeConsumed && !_focusGroupEscapeConsumed)
                    {
                        state.Draft = widget.Format(widget.Value.Value);
                        state.Editing = false;
                    }
                }
            }
            edit = ReconcileInputEdit(root, state.Draft, edit, preserveHistory: increment.Activated || decrement.Activated) with
            {
                FocusWithin = _state.FocusedId == root
            };
            options = options with
            {
                Invalid = options.Invalid || state.Editing && !widget.Parse(state.Draft).Success
            };
            if (_state.FocusedId == root && IsControlEnabled && !options.ReadOnly && _popupTierDepth >= _blockingKeyboardTier)
            {
                increment = increment with
                {
                    Held = increment.Held || IsKeyDown(UiKey.UpArrow) || IsKeyDown(UiKey.PageUp)
                };
                decrement = decrement with
                {
                    Held = decrement.Held || IsKeyDown(UiKey.DownArrow) || IsKeyDown(UiKey.PageDown)
                };
            }
            TPresentation.Paint(context, layout, edit, options, increment, decrement,
                !options.ReadOnly && numbers.CanMove(widget.Value.Value, 1), !options.ReadOnly && numbers.CanMove(widget.Value.Value, -1));
            var value = widget.Value.Value;
            ExposeAccessibility(root, _accessibilityParent, UiAccessibilityRole.SpinButton, widget.Label, bounds, state.Draft,
                options.ReadOnly, widget.MaxLength, invalid: options.Invalid,
                range: value is { } amount ? new UiAccessibilityRangeInfo(amount, numbers.Minimum, numbers.Maximum, numbers.Step, numbers.PageStep) : null,
                textInfo: edit.AccessibilityText, textLayout: CaptureInputTextLayout(root, edit.AccessibilityText, layout.Input, edit));
            void ExposeStepper(string resolved, string label, UiRect rect, bool enabled)
            {
                var owner = _accessibilityWidget;
                BeginDisabled(!enabled);
                try
                {
                    _accessibilityWidget = null;
                    ExposeAccessibility(resolved, root, UiAccessibilityRole.Button, label, rect, focusable: false, valuePattern: false);
                }
                finally { _accessibilityWidget = owner; EndDisabled(); }
            }
            ExposeStepper(incrementId, options.IncrementLabel, layout.Increment, !options.ReadOnly && numbers.CanMove(value, 1));
            ExposeStepper(decrementId, options.DecrementLabel, layout.Decrement, !options.ReadOnly && numbers.CanMove(value, -1));
        }
        finally { FinishLayoutBox(origin, size); }
    }
}
