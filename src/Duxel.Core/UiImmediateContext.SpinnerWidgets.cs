namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    public UiVector2 MeasureSpinner<TPresentation>(string? label = null, UiSpinnerOptions options = default) where TPresentation : IUiSpinnerPresentation
        => _ui.MeasureSpinner<TPresentation>(label, options);
    public void AnnularSector(UiVector2 center, float radius, float stroke, float start, float sweep, UiColor first, UiColor last) =>
        _ui.PaintAnnularSector(center, radius, stroke, start, sweep, first, last);
}
public sealed partial class UiImmediateContext
{
    internal void PaintAnnularSector(UiVector2 center, float radius, float stroke, float start, float sweep, UiColor first, UiColor last) =>
        _builder.AddAnnularSector(center, radius, stroke, start, sweep, first, last, _whiteTexture, CurrentClipRect);
    public void Spinner<TBehavior, TPresentation>(string id, ref TBehavior behavior, string? label = null, UiSpinnerOptions options = default)
        where TBehavior : struct, IUiSpinnerBehavior<TBehavior> where TPresentation : IUiSpinnerPresentation
        => Spinner<TBehavior, TPresentation>(id, ref behavior, label, options, false);
    public void Spinner<TBehavior, TPresentation>(string id, ref TBehavior behavior, string? label, UiSpinnerOptions options, bool constrainLayout)
        where TBehavior : struct, IUiSpinnerBehavior<TBehavior> where TPresentation : IUiSpinnerPresentation
    {
        EnsureNotMeasuring();
        options.Validate();
        var frame = TBehavior.Evaluate(ref behavior, options, GetTime());
        if (!frame.Visible)
        {
            var size = new UiVector2(options.Width > 0 ? options.Width : GetContentRegionAvail().X, 0);
            if (constrainLayout)
                size = ConstrainToLayout(size);
            var position = AdvanceCursor(size);
            if (frame.NextFrameTimeSeconds is { } deadline)
                RequestFrameAt(deadline);
            _lastItemPos = position;
            _lastItemSize = size;
            _lastItemId = ResolveId(id);
            _lastItemEdited = false;
            ExposeAccessibility(_lastItemId, _accessibilityParent, UiAccessibilityRole.ProgressBar, string.Empty, new(position.X, position.Y, size.X, 0), focusable : false, valuePattern : false);
            return;
        }
        var context = new UiControlContext(this);
        using var text = TPresentation.BeginLabel(context, options, !string.IsNullOrEmpty(label));
        var layout = ResolveSpinnerLayout<TPresentation>(context, label, options, constrainLayout);
        var origin = AdvanceCursor(layout.Size);
        _lastItemPos = origin;
        _lastItemSize = layout.Size;
        _lastItemId = ResolveId(id);
        _lastItemEdited = false;
        var bounds = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        var clip = IntersectRect(CurrentClipRect, bounds);
        if (clip.Width > 0 && clip.Height > 0)
        {
            if (!options.Paused) RequestControlAnimation(bounds);
            using var rootClip = context.Clip(bounds);
            TPresentation.Paint(context, options, frame, layout, origin);
        }
        ExposeAccessibility(_lastItemId, _accessibilityParent, UiAccessibilityRole.ProgressBar, label ?? string.Empty, bounds, focusable : false, valuePattern : false);
    }
    internal UiVector2 MeasureSpinner<TPresentation>(string? label, UiSpinnerOptions options) where TPresentation : IUiSpinnerPresentation
    {
        options.Validate();
        var context = new UiControlContext(this);
        using var text = TPresentation.BeginLabel(context, options, !string.IsNullOrEmpty(label));
        return ResolveSpinnerLayout<TPresentation>(context, label, options, true).Size;
    }
    private UiSpinnerLayout ResolveSpinnerLayout<TPresentation>(UiControlContext context, string? label, UiSpinnerOptions options, bool constrainLayout)
        where TPresentation : IUiSpinnerPresentation
    {
        var layout = TPresentation.Measure(context, label, options);
        if (!float.IsFinite(layout.Size.X) || !float.IsFinite(layout.Size.Y) || layout.Size.X < 0 || layout.Size.Y < 0)
            throw new InvalidOperationException("Spinner presentation returned invalid layout.");
        if (!constrainLayout)
            return layout;
        var allocated = ConstrainToLayout(layout.Size);
        if (allocated == layout.Size)
            return layout;
        var arranged = TPresentation.Arrange(context, options, layout, allocated);
        if (arranged.Size != allocated)
            throw new InvalidOperationException("Spinner presentation did not honor the parent allocation.");
        return arranged;
    }
}
