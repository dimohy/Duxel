namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    public UiVector2 MeasureProgress<TPresentation>(UiProgressValue value, UiVector2 size)
        where TPresentation : IUiProgressPresentation => _ui.MeasureProgress<TPresentation>(value, size);

    public UiVector2 MeasureProgress<TPresentation>(UiVector2 size)
        where TPresentation : IUiProgressPresentation => _ui.MeasureProgress<TPresentation>(size);
}

public sealed partial class UiImmediateContext
{
    public void ProgressBar<TPresentation>(UiProgressValue value, UiVector2 size = default)
        where TPresentation : IUiProgressPresentation => RenderProgress<TPresentation>(value.Validate(), size);
    public void ProgressBar<TPresentation>(string id, UiProgressValue value, UiVector2 size = default)
        where TPresentation : IUiProgressPresentation
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        RenderProgress<TPresentation>(value.Validate(), size, accessibilityId: id);
    }
    public void ProgressBar<TPresentation>(UiProgressValue value, UiVector2 size, bool constrainLayout)
        where TPresentation : IUiProgressPresentation => RenderProgress<TPresentation>(value.Validate(), size, constrainLayout: constrainLayout);
    public void ProgressBar<TBehavior, TPresentation>(ref TBehavior behavior, UiProgressValue value, UiVector2 size = default)
        where TBehavior : struct, IUiProgressBehavior<TBehavior> where TPresentation : IUiProgressPresentation =>
        RenderProgress<TBehavior, TPresentation>(ref behavior, value.Validate(), size);
    public void ProgressBar<TBehavior, TPresentation>(ref TBehavior behavior, UiProgressValue value, UiVector2 size, bool constrainLayout)
        where TBehavior : struct, IUiProgressBehavior<TBehavior> where TPresentation : IUiProgressPresentation =>
        RenderProgress<TBehavior, TPresentation>(ref behavior, value.Validate(), size, constrainLayout);
    public void ProgressBar<TBehavior, TPresentation>(string id, ref TBehavior behavior, UiProgressValue value, UiVector2 size = default)
        where TBehavior : struct, IUiProgressBehavior<TBehavior> where TPresentation : IUiProgressPresentation
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        RenderProgress<TBehavior, TPresentation>(ref behavior, value.Validate(), size, accessibilityId: id);
    }
    internal void RenderProgress<TBehavior, TPresentation>(ref TBehavior behavior, UiProgressValue value, UiVector2 size, bool constrainLayout = false,
        string? accessibilityId = null)
        where TBehavior : struct, IUiProgressBehavior<TBehavior> where TPresentation : IUiProgressPresentation
    {
        EnsureNotMeasuring();
        ValidateProgressSize(size);
        var displayed = TBehavior.Evaluate(ref behavior, value, GetTime(), out var animating);
        RenderProgress<TPresentation>(displayed, size, animating, constrainLayout, accessibilityId, value);
    }
    internal void RenderProgress<TPresentation>(UiProgressValue value, UiVector2 size, bool animating = false, bool constrainLayout = false,
        string? accessibilityId = null, UiProgressValue? semanticValue = null)
        where TPresentation : IUiProgressPresentation
    {
        EnsureNotMeasuring();
        ValidateProgressSize(size);
        var context = new UiControlContext(this);
        var natural = TPresentation.Measure(context, value, size);
        var bounds = PlaceProgress(ResolveProgressLayout<TPresentation>(context, natural, constrainLayout));
        var visible = IntersectRect(CurrentClipRect, bounds);
        if (visible.Width > 0 && visible.Height > 0)
        {
            if (animating)
                context.RequestAnimation(bounds);
            using var rootClip = context.Clip(bounds);
            TPresentation.Paint(context, value, bounds);
        }
        if (_accessibility is not null && accessibilityId is not null)
        {
            var semantic = semanticValue ?? value;
            var resolvedId = ResolveId(accessibilityId);
            ClaimAccessibilityField(resolvedId);
            ExposeAccessibility(resolvedId, _accessibilityParent, UiAccessibilityRole.ProgressBar,
                accessibilityId == UiControlWidget.PrimaryControlId ? string.Empty : accessibilityId, bounds,
                readOnly: true, focusable: false, valuePattern: false,
                range: semantic.Value is { } numeric
                    ? new UiAccessibilityRangeInfo(Math.Clamp(numeric, 0, semantic.Maximum), 0, semantic.Maximum, 0, 0)
                    : null);
        }
    }
    internal UiVector2 MeasureProgress<TPresentation>(UiProgressValue value, UiVector2 size)
        where TPresentation : IUiProgressPresentation
    {
        value.Validate();
        ValidateProgressSize(size);
        var context = new UiControlContext(this);
        return ResolveProgressLayout<TPresentation>(context, TPresentation.Measure(context, value, size), true);
    }
    internal UiVector2 MeasureProgress<TPresentation>(UiVector2 size)
        where TPresentation : IUiProgressPresentation
    {
        ValidateProgressSize(size);
        var context = new UiControlContext(this);
        return ResolveProgressLayout<TPresentation>(context, TPresentation.Measure(context, size), true);
    }
    private UiVector2 ResolveProgressLayout<TPresentation>(UiControlContext context, UiVector2 natural, bool constrainLayout)
        where TPresentation : IUiProgressPresentation
    {
        if (!float.IsFinite(natural.X) || !float.IsFinite(natural.Y) || natural.X < 0 || natural.Y < 0)
            throw new InvalidOperationException("Progress presentation returned invalid extents.");
        if (!constrainLayout)
            return natural;
        var allocated = ConstrainToLayout(natural);
        if (allocated == natural)
            return natural;
        var arranged = TPresentation.Arrange(context, natural, allocated);
        if (arranged != allocated)
            throw new InvalidOperationException("Progress presentation did not honor the parent allocation.");
        return arranged;
    }
    private static void ValidateProgressSize(UiVector2 size)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X < 0 || size.Y < 0)
            throw new ArgumentOutOfRangeException(nameof(size));
    }
    private UiRect PlaceProgress(UiVector2 size)
    {
        var cursor = AdvanceCursor(size);
        return new(cursor.X, cursor.Y, size.X, size.Y);
    }
    internal void RequestControlAnimation()
    {
        _state.MarkControlAnimation();

    }
    internal void RequestControlAnimation(UiRect bounds)
    {
        var visible = IsControlAnimationVisible(bounds);
        TrackLayerAnimationBounds(bounds);
        _state.MarkControlAnimation(visible, bounded: true);

    }
    internal void PaintProgressGradient(UiRect bar, UiRect bounds, UiColor edge, UiColor center, float radius)
    {
        _builder.AddRectFilledGradient(bounds, new(new(bar.X, bar.Y), new(bar.X + bar.Width, bar.Y),
            edge, center, edge), _whiteTexture, CurrentClipRect, radius);
    }
}
