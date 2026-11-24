namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    public UiFieldContext? Field => _ui.CurrentField;
    public bool FieldLabelPressed(string id, UiRect bounds) => _ui.FieldLabelPressed(id, bounds);
    public UiVector2 MeasureField<TPresentation>(UiWidget control, UiVector2? controlSize, float width, UiFieldOptions options, UiFieldContent<TPresentation> content)
        where TPresentation : IUiFieldPresentation => _ui.MeasureField(control, controlSize, width, options, content);
    internal UiVector2 MeasureOwnedField<TPresentation>(UiWidget owner, UiWidget control, UiVector2? controlSize, float width,
        UiFieldOptions options, UiFieldContent<TPresentation> content, ref UiRetainedLayout<UiVector2>? retained)
        where TPresentation : IUiFieldPresentation => _ui.MeasureField(owner, control, controlSize, width, options, content, ref retained);
}

public sealed partial class UiImmediateContext
{
    private UiFieldContext? _currentField;
    private bool _fieldFocusRequested;
    private string? _fieldFocusedControlId;
    public UiFieldContext? CurrentField => _currentField;

    private UiVector2 MeasureFieldControl<TPresentation>(UiWidget control, float width, UiFieldOptions options, bool constrainLayout) where TPresentation : IUiFieldPresentation
    {
        UiRetainedLayout<UiVector2>? unused = null;
        return MeasureFieldControl<TPresentation>(null, control, width, options, constrainLayout, ref unused);
    }
    private UiVector2 MeasureFieldControl<TPresentation>(UiWidget? owner, UiWidget control, float width, UiFieldOptions options,
        bool constrainLayout, ref UiRetainedLayout<UiVector2>? retained) where TPresentation : IUiFieldPresentation
    {
        ArgumentNullException.ThrowIfNull(control);
        options.Validate();
        if (!float.IsFinite(width) || width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        var available = GetContentRegionAvail();
        var fieldWidth = width > 0 ? width : MathF.Max(1, available.X);
        if (constrainLayout)
            fieldWidth = ConstrainLayoutWidth(fieldWidth);
        var controlWidth = TPresentation.ControlWidth(fieldWidth);
        ValidateFieldControlWidth(controlWidth, fieldWidth == 0);
        var previous = _currentField;
        _currentField = new(options.Label, options.Hint, options.ValidationMessage, options.Required, options.EffectiveValidationState);
        BeginDisabled(options.Disabled);
        try
        {
            var constrainedChild = constrainLayout && (IsMeasuring ? _measureConstraints.HasValue
                : _layoutChildConstraints && _inputSlotBoundsTier == _popupTierDepth && _inputSlotBounds.HasValue);
            // Reserve Field text rows before allocating the control's height.
            var childConstraints = new UiBoxConstraints(controlWidth, controlWidth, 0, float.PositiveInfinity);
            var desired = constrainedChild
                ? owner is null ? control.MeasureConstrained(this, childConstraints)
                    : UiWidget.MeasureOwnedChild(owner, control, new(this), childConstraints, ref retained, !IsMeasuring)
                : control.Measure(this, new(controlWidth, available.Y));
            if (desired.Y <= 0)
                throw new InvalidOperationException("A naturally measured Field control must have positive height.");
            return new(controlWidth, desired.Y);
        }
        finally
        {
            EndDisabled();
            _currentField = previous;
        }
    }

    private UiVector2 ConstrainFieldControlSize<TPresentation>(UiVector2 controlSize, bool constrainLayout) where TPresentation : IUiFieldPresentation
    {
        if (!float.IsFinite(controlSize.X) || !float.IsFinite(controlSize.Y) || controlSize.X <= 0 || controlSize.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(controlSize));
        var constrainedWidth = IsMeasuring
            ? _measureConstraints is { } constraints && (constraints.MinWidth > 0 || float.IsFinite(constraints.MaxWidth))
            : _layoutChildConstraints && _inputSlotBoundsTier == _popupTierDepth && _inputSlotBounds.HasValue;
        if (!constrainLayout || !constrainedWidth)
            return controlSize;
        var naturalWidth = TPresentation.FieldWidth(controlSize.X);
        if (!float.IsFinite(naturalWidth) || naturalWidth <= 0)
            throw new InvalidOperationException("Field presentation returned invalid natural field width.");
        var allocatedWidth = ConstrainLayoutWidth(naturalWidth);
        if (allocatedWidth == naturalWidth)
            return controlSize;
        var width = TPresentation.ControlWidth(allocatedWidth);
        ValidateFieldControlWidth(width, allocatedWidth == 0);
        return controlSize with { X = width };
    }

    private static void ValidateFieldControlWidth(float width, bool allowZero)
    {
        if (!float.IsFinite(width) || width < 0 || width == 0 && !allowZero)
            throw new InvalidOperationException("Field presentation returned invalid control width.");
    }

    public void AutoField<TBehavior, TPresentation>(UiWidget control, float width = 0, UiFieldOptions options = default, UiFieldContent<TPresentation>? content = null)
        where TBehavior : IUiFieldBehavior where TPresentation : IUiFieldPresentation =>
        AutoField<TBehavior, TPresentation>(control, width, options, content, false);

    public void AutoField<TBehavior, TPresentation>(UiWidget control, float width, UiFieldOptions options, UiFieldContent<TPresentation>? content, bool constrainLayout)
        where TBehavior : IUiFieldBehavior where TPresentation : IUiFieldPresentation
    {
        EnsureNotMeasuring();
        var size = MeasureFieldControl<TPresentation>(control, width, options, constrainLayout);
        RenderField<TBehavior, TPresentation>(control, size, options, content, constrainLayout);
    }

    internal UiVector2 MeasureField<TPresentation>(UiWidget control, UiVector2? controlSize, float width, UiFieldOptions options, UiFieldContent<TPresentation> content)
        where TPresentation : IUiFieldPresentation
    {
        UiRetainedLayout<UiVector2>? unused = null;
        return MeasureField(null, control, controlSize, width, options, content, ref unused);
    }
    internal void AutoOwnedField<TBehavior, TPresentation>(UiWidget owner, UiWidget control, float width, UiFieldOptions options,
        UiFieldContent<TPresentation> content, ref UiRetainedLayout<UiVector2>? retained)
        where TBehavior : IUiFieldBehavior where TPresentation : IUiFieldPresentation
    {
        EnsureNotMeasuring();
        var size = MeasureFieldControl<TPresentation>(owner, control, width, options, true, ref retained);
        RenderField<TBehavior, TPresentation>(control, size, options, content, true);
    }
    internal UiVector2 MeasureField<TPresentation>(UiWidget? owner, UiWidget control, UiVector2? controlSize, float width,
        UiFieldOptions options, UiFieldContent<TPresentation> content, ref UiRetainedLayout<UiVector2>? retained)
        where TPresentation : IUiFieldPresentation
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(content);
        options.Validate();
        if (!float.IsFinite(width) || width < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        var size = controlSize is { } requested ? ConstrainFieldControlSize<TPresentation>(requested, true)
            : MeasureFieldControl<TPresentation>(owner, control, width, options, true, ref retained);
        try
        {
            return ResolveFieldLayout(options, size, content, true).Size;
        }
        finally
        {
            content.EndFrame();
        }
    }

    internal bool FieldLabelPressed(string id, UiRect bounds)
    {
        EnsureNotMeasuring();
        return IsControlEnabled && bounds.Width > 0 && bounds.Height > 0 && ItemHoverable(ResolveId(id), bounds) && _leftMousePressed;
    }

    private void ClaimFieldFocus(string id, UiRect bounds, bool allowDisabledFocus = false)
    {
        var standalone = HasLabelFocusRequest();
        if (!_fieldFocusRequested && !standalone)
            return;
        if (standalone) { _labelFocusTarget = null; _labelFocusOwner = null; }
        if ((!IsControlEnabled && !allowDisabledFocus) || _popupTierDepth < _blockingKeyboardTier)
            return;
        var clipped = IntersectRect(CurrentClipRect, bounds);
        if (clipped.Width <= 0 || clipped.Height <= 0)
            return;
        if (_state.ActiveId is { } previous && _state.HasTextInputStorage(previous))
            _imeHandler?.SetCompositionOwner(null);
        _state.ActiveId = null;
        _state.PendingTextInputFocusId = null;
        _state.FocusedId = id;
        _ownedControlFocus = id;
        _state.NavCursorVisible = false;
        CancelControlSpace();
        _fieldFocusedControlId = id;
        _fieldFocusRequested = false;
        _requestFrame?.Invoke();
    }

    public void Field<TBehavior, TPresentation>(UiWidget control, UiVector2 controlSize, UiFieldOptions options = default, UiFieldContent<TPresentation>? content = null)
        where TBehavior : IUiFieldBehavior where TPresentation : IUiFieldPresentation =>
        Field<TBehavior, TPresentation>(control, controlSize, options, content, false);

    public void Field<TBehavior, TPresentation>(UiWidget control, UiVector2 controlSize, UiFieldOptions options, UiFieldContent<TPresentation>? content, bool constrainLayout)
        where TBehavior : IUiFieldBehavior where TPresentation : IUiFieldPresentation
    {
        EnsureNotMeasuring();
        ArgumentNullException.ThrowIfNull(control);
        options.Validate();
        var size = ConstrainFieldControlSize<TPresentation>(controlSize, constrainLayout);
        RenderField<TBehavior, TPresentation>(control, size, options, content, constrainLayout);
    }

    private UiFieldLayout ResolveFieldLayout<TPresentation>(UiFieldOptions options, UiVector2 controlSize, UiFieldContent<TPresentation>? content, bool constrainLayout)
        where TPresentation : IUiFieldPresentation
    {
        var context = new UiControlContext(this);
        if (content is not null)
            content.Measure(this, context, options, controlSize, CurrentControlTextLayoutKey, _typographyFonts, CanRetainControlTextLayout);
        var natural = content is null ? TPresentation.Measure(context, options, controlSize) : content.Layout;
        ValidateFieldLayout(natural, constrainLayout);
        if (!constrainLayout)
            return natural;
        var allocated = ConstrainToLayout(natural.Size);
        if (allocated == natural.Size)
            return natural;
        var arranged = TPresentation.Arrange(context, options, natural, allocated);
        ValidateFieldLayout(arranged, true);
        if (arranged.Size != allocated)
            throw new InvalidOperationException("Field presentation did not honor the parent allocation.");
        return arranged;
    }

    private void RenderField<TBehavior, TPresentation>(UiWidget control, UiVector2 controlSize, UiFieldOptions options, UiFieldContent<TPresentation>? content, bool constrainLayout)
        where TBehavior : IUiFieldBehavior where TPresentation : IUiFieldPresentation
    {
        var context = new UiControlContext(this);
        try
        {
            var layout = ResolveFieldLayout(options, controlSize, content, constrainLayout);
            var origin = AdvanceCursor(layout.Size);
            var root = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
            var assigned = constrainLayout && _layoutChildConstraints && _inputSlotBoundsTier == _popupTierDepth && _inputSlotBounds.HasValue;
            using var rootClip = assigned ? context.Clip(root) : default;
            var label = OffsetFieldRect(layout.LabelTarget ?? layout.Label, origin);
            var bounds = OffsetFieldRect(layout.Control, origin);
            var previous = (_currentField, _fieldFocusRequested, _fieldFocusedControlId);
            var previousSemanticField = _accessibilityFieldTarget;
            var request = TBehavior.FocusRequested(context, "\u001ffield-label", label);
            // Field.Disabled affects only the actual control, not the label or descriptions.
            TPresentation.Paint(context, options, layout, origin);
            _currentField = new(options.Label, options.Hint, options.ValidationMessage, options.Required, options.EffectiveValidationState);
            _fieldFocusRequested = request;
            _fieldFocusedControlId = null;
            BeginDisabled(options.Disabled);
            _accessibilityFieldTarget = null;
            try
            {
                RenderAllocatedChild(_builder, control, bounds, root, constrainLayout : assigned);
            }
            finally
            {
                EndDisabled();
                (_currentField, _fieldFocusRequested, _fieldFocusedControlId) = previous;
                _accessibilityFieldTarget = previousSemanticField;
            }
            _lastItemPos = origin;
            _lastItemSize = layout.Size;
            _lastItemId = null;
            _lastItemEdited = false;
        }
        finally
        {
            content?.EndFrame();
        }
    }

    internal static UiRect OffsetFieldRect(UiRect rect, UiVector2 origin) => rect with { X = rect.X + origin.X, Y = rect.Y + origin.Y };

    private static void ValidateFieldLayout(in UiFieldLayout layout, bool allowEmpty = false)
    {
        static bool Rect(UiRect r) => float.IsFinite(r.X) && float.IsFinite(r.Y) && float.IsFinite(r.Width) && float.IsFinite(r.Height) && r.Width >= 0 && r.Height >= 0;
        if (!float.IsFinite(layout.Size.X) || !float.IsFinite(layout.Size.Y) || layout.Size.X < 0 || layout.Size.Y < 0
            || !allowEmpty && (layout.Size.X == 0 || layout.Size.Y == 0)
            || !Rect(layout.Control) || !Rect(layout.Label) || !Rect(layout.RequiredIndicator) || !Rect(layout.Validation) || !Rect(layout.ValidationIcon) || !Rect(layout.Hint)
            || layout.LabelTarget is { } target && !Rect(target) || layout.LabelClip is { } clip && !Rect(clip))
            throw new InvalidOperationException("Field presentation returned invalid layout.");
    }
}
