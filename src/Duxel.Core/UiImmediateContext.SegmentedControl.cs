namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    internal void RenderSegments(UiSegmentedControlWidget widget, string id)
    {
        var context = new UiControlContext(this);
        using var font = context.BeginTypography<UiRegularTypeface, UiSegmentedControlTypography>();
        var options = widget.Options;
        var width = ConstrainLayoutWidth(options.Width > 0 ? options.Width : MathF.Max(0, GetContentRegionAvail().X));
        var size = context.ConstrainToLayout(widget.Prepare(this, context, width));
        var origin = AdvanceCursor(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        if (size.X <= 0 || size.Y <= 0) return;
        using var clip = context.Clip(bounds);
        if (widget.Value.Value is { } existing) widget.Commit(existing);
        if (widget.Scroll.Selected != widget.Value.Value) widget.Reveal(widget.Value.Value, bounds.Width);
        widget.Scroll.Selected = widget.Value.Value;
        var previousOffset = widget.Scroll.Offset;
        ProcessHorizontalScroll(bounds, 0, widget.Scroll);
        if (previousOffset != widget.Scroll.Offset) _requestFrame?.Invoke();
        widget.ArrangeInput();
        var interaction = ProcessRadioGroup(id, bounds, widget.RadioItems, widget.InputLayouts, widget.Value.Value,
            widget.Input, UiButtonActivation.Release, options.RightToLeft || RightToLeft, widget.Scroll.SuppressClick);
        interaction = ApplyRadioAccessibility(id, widget.Label, bounds, widget.RadioItems, widget.InputLayouts,
            widget.Input, interaction, selectionRequired : false, valueBinding : widget.Selection, beforePublication : widget.BeforePublication);
        var selected = -1;
        for (var i = 0; i < widget.Items.Length; i++)
            if (widget.Items[i].Value == widget.Value.Value)
            {
                selected = i;
                break;
            }
        var thumb = default(UiRect);
        if (selected >= 0)
        {
            var target = widget.Layouts[selected].Bounds;
            var settings = UiSegmentedControlMotion.Options(options.ReducedMotion);
            thumb = new(AnimateFloat(ref widget.ThumbX, target.X, settings), 0,
                AnimateFloat(ref widget.ThumbWidth, target.Width, settings), bounds.Height);
        }
        else
        {
            widget.ThumbX = default;
            widget.ThumbWidth = default;
        }
        if (options.IsFixedWidth)
        {
            UiSegmentedControlPainter.Paint(context, widget, bounds,
                thumb with { X = bounds.X + thumb.X, Y = bounds.Y }, selected, interaction);
            return;
        }
        var source = new UiRect(0, 0, widget.ContentWidth, bounds.Height);
        var version = widget.PreparePaint(context, thumb, selected, interaction);
        var mask = context.Theme.ForcedColors is not null ? default : new UiHorizontalOpacityMask(
            widget.ScrollOffset > .01f ? .25f : 0, widget.ScrollOffset < widget.MaximumScrollOffset - .01f ? .25f : 0);
        var uv = new UiImageUvTransform(new(widget.ScrollOffset / source.Width, 0), new(bounds.Width / source.Width, 0), new(0, 1));
        context.PaintGroup(widget.PaintGroup, source, version, widget.PaintContent, uv, mask, destination : bounds);
    }
}
