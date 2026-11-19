namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    public bool InputTextMultiline<TBehavior, TPresentation>(string label, ref string value, int maxLength,
        float width = 0, float height = 0, UiMultilineInputOwner? owner = null, bool constrainLayout = false)
        where TBehavior : IUiMultilineInputBehavior where TPresentation : IUiMultilineInputPresentation =>
        InputTextMultiline<TBehavior, TPresentation>(label, label, ref value, maxLength, width, height, owner, constrainLayout);

    public bool InputTextMultiline<TBehavior, TPresentation>(string label, ref string value, int maxLength,
        UiMultilineInputOptions options, float width = 0, float height = 0,
        UiMultilineInputOwner? owner = null, bool constrainLayout = false)
        where TBehavior : IUiMultilineInputBehavior where TPresentation : IUiMultilineInputPresentation =>
        InputTextMultiline<TBehavior, TPresentation>(label, label, ref value, maxLength, width, height, owner, constrainLayout, options);

    internal bool InputTextMultiline<TBehavior, TPresentation>(string id, string label, ref string value, int maxLength,
        float width, float height, UiMultilineInputOwner? owner, bool constrainLayout, UiMultilineInputOptions options = default)
        where TBehavior : IUiMultilineInputBehavior where TPresentation : IUiMultilineInputPresentation
    {
        EnsureNotMeasuring();
        ArgumentNullException.ThrowIfNull(label);
        if (maxLength <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        owner?.Validate();
        var context = new UiControlContext(this);
        using var typography = TPresentation.BeginText(context);
        var local = PrepareMultiline<TPresentation>(context, width, height, constrainLayout, options);
        var origin = AdvanceCursor(local.Size);
        var layout = local.At(origin);
        layout.Validate();
        using var clip = BeginControlClip(layout.Field);
        var resolvedId = ResolveId(id);
        owner?.Bind(this, resolvedId);
        layout = layout with
        {
            InputOwner = owner
        };
        if (layout.Wrap)
            layout = layout with
            {
                VisualLines = owner?.Lines ?? new UiVisualTextLines()
            };
        ClaimAccessibilityField(resolvedId);
        var parent = _accessibilityParent;
        _accessibilityParent = resolvedId;
        try
        {
            ClaimFieldFocus(resolvedId, layout.Field);
            var edit = TBehavior.Process(context, resolvedId, ref value, maxLength, layout);
            if (TBehavior.AccessibilityValue)
                edit = ApplyMultilineAccessibilityScroll(resolvedId, layout, edit);
            TPresentation.Paint(context, layout, edit);
            CompleteMultilineInput(resolvedId, layout, edit, owner?.Scrollbars);
            var content = edit.VisualContent ?? layout.ContentBounds;
            var scroll = new UiAccessibilityScrollInfo(new(content.Width, content.Height), new(edit.ContentWidth, edit.ContentHeight), new(edit.ScrollX, edit.ScrollY));
            ExposeAccessibility(resolvedId, parent, UiAccessibilityRole.Edit, label, layout.Field, value,
                layout.Options.ReadOnly, maxLength, TBehavior.AccessibilityFocus, TBehavior.AccessibilityValue,
                scroll: TBehavior.AccessibilityValue && CanPublishScroll(IntersectRect(CurrentClipRect, content), scroll) ? scroll : null,
                multiline: true, invalid: layout.Options.Invalid, textInfo: TBehavior.AccessibilityValue ? edit.AccessibilityText : null,
                textLayout: TBehavior.AccessibilityValue ? CaptureMultilineTextLayout(resolvedId, edit.AccessibilityText, layout, edit) : null);
            if (TBehavior.AccessibilityValue)
                _accessibility?.CaptureScrollViewport(resolvedId, content, CurrentClipRect, scroll);
            _lastItemPos = origin;
            _lastItemSize = local.Size;
            _lastItemId = resolvedId;
            _lastItemFlags = _currentItemFlags;
            _lastItemEdited = edit.Changed;
            return edit.Changed;
        }
        finally
        {
            _accessibilityParent = parent;
        }
    }

    internal UiVector2 MeasureMultiline<TPresentation>(float width, float height, UiMultilineInputOptions options = default)
        where TPresentation : IUiMultilineInputPresentation
    {
        var context = new UiControlContext(this);
        using var typography = TPresentation.BeginText(context);
        return PrepareMultiline<TPresentation>(context, width, height, true, options).Size;
    }

    private UiMultilineInputLayout PrepareMultiline<TPresentation>(UiControlContext context,
        float width, float height, bool constrainLayout, UiMultilineInputOptions options) where TPresentation : IUiMultilineInputPresentation
    {
        if (!float.IsFinite(width) || width < 0 || !float.IsFinite(height) || height < 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Multiline extents must be finite and nonnegative.");
        options.Validate();
        options = options with
        {
            Invalid = options.Invalid || (_currentField?.Invalid ?? false)
        };
        var natural = TPresentation.Measure(context, width, height);
        natural = natural with
        {
            Options = options,
            Wrap = options.Wrap ?? natural.Wrap
        };
        natural.Validate();
        if (natural.Field.X != 0 || natural.Field.Y != 0)
            throw new InvalidOperationException("A multiline presentation must measure its field at the local origin.");
        if (!constrainLayout)
            return natural;
        var size = CurrentLayoutConstraints.Constrain(natural.Size);
        if (size == natural.Size)
            return natural;
        var allocated = TPresentation.Arrange(context, natural, size);
        allocated = allocated with
        {
            Options = options,
            Wrap = options.Wrap ?? allocated.Wrap
        };
        allocated.Validate();
        if (allocated.Field.X != 0 || allocated.Field.Y != 0 || allocated.Size != size)
            throw new InvalidOperationException("Multiline arrangement changed the assigned root allocation.");
        return allocated;
    }
}
