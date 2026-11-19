using System.Numerics;

namespace Duxel.Core;

internal readonly ref struct UiSkeletonScope(UiImmediateContext owner, UiSkeletonStyle? previous, object resources)
{
    public void Dispose() => owner.RestoreSkeletonScope(previous, resources);
}

public readonly partial struct UiControlContext
{
    public UiSkeletonStyle SkeletonStyle => _ui.SkeletonStyle;
    internal UiSkeletonScope SkeletonScope(UiSkeletonOptions options, UiLayoutEnvironment<UiSkeletonStyle> environment)
        => _ui.BeginSkeletonScope(options, environment);
}

public sealed partial class UiImmediateContext
{
    private UiSkeletonStyle? _skeletonStyle;
    internal UiSkeletonStyle SkeletonStyle => _skeletonStyle ?? UiSkeletonStyle.Default;

    internal UiSkeletonScope BeginSkeletonScope(UiSkeletonOptions options, UiLayoutEnvironment<UiSkeletonStyle> environment)
    {
        var previous = _skeletonStyle;
        var resources = _layoutMeasurementResources;
        var style = options.Resolve(SkeletonStyle);
        _layoutMeasurementResources = environment.Resolve(resources, style);
        _skeletonStyle = style;
        return new(this, previous, resources);
    }

    internal void RestoreSkeletonScope(UiSkeletonStyle? previous, object resources)
    {
        _skeletonStyle = previous;
        _layoutMeasurementResources = resources;
    }

    internal void RenderSkeleton(string id, string label, UiWidget? child, UiContainerLayoutResult layout)
    {
        EnsureNotMeasuring();
        var origin = AdvanceCursor(layout.Size);
        var root = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        var childBounds = layout.Child with
        {
            X = origin.X + layout.Child.X,
            Y = origin.Y + layout.Child.Y
        };
        var resolved = ResolveId(id);
        var parent = _accessibilityParent;
        ExposeAccessibility(resolved, parent, UiAccessibilityRole.ProgressBar, label, root,
            focusable: false, valuePattern: false, busy: true);
        _accessibilityParent = resolved;
        try
        {
            using var clip = BeginControlClip(root);
            if (child is not null)
                RenderAllocatedChild(_builder, child, childBounds, root, constrainLayout: true);
        }
        finally { _accessibilityParent = parent; }
        _lastItemPos = origin;
        _lastItemSize = layout.Size;
        _lastItemId = resolved;
        _lastItemEdited = false;
    }

    internal void RenderSkeletonItem<TPresentation>(UiSkeletonOptions options) where TPresentation : IUiSkeletonPresentation
    {
        EnsureNotMeasuring();
        var context = new UiControlContext(this);
        var style = options.Resolve(SkeletonStyle);
        var size = ConstrainToLayout(TPresentation.Measure(context, style, options.Width));
        var origin = AdvanceCursor(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var visible = IntersectRect(CurrentClipRect, bounds);
        if (visible.Width > 0 && visible.Height > 0)
        {
            var frame = UiSkeletonMotion.Sample(style, GetTime());
            if (frame.Animated)
                RequestControlAnimation(bounds);
            using var clip = BeginControlClip(bounds);
            PaintSkeleton<TPresentation>(context, style, frame, bounds);
        }
        _lastItemPos = origin;
        _lastItemSize = size;
        _lastItemId = null;
        _lastItemEdited = false;
    }

    private void PaintSkeleton<TPresentation>(UiControlContext context, UiSkeletonStyle style,
        UiSkeletonFrame frame, UiRect bounds) where TPresentation : IUiSkeletonPresentation
    {
        if (style.Shape != UiSkeletonShape.Circle || bounds.Width == bounds.Height)
        {
            TPresentation.Paint(context, style, frame, bounds);
            return;
        }
        // CSS 50% corner radii become an ellipse under a nonsquare allocation.
        // Reuse the common affine primitive path, including its parent masks.
        var local = new UiRect(0, 0, style.Size, style.Size);
        var matrix = Matrix3x2.CreateScale(bounds.Width / style.Size, bounds.Height / style.Size)
            * Matrix3x2.CreateTranslation(bounds.X, bounds.Y);
        var parent = _builder;
        var clip = CurrentClipRect;
        using var coordinates = BeginWidgetAffineSpace(matrix, MathF.Max(bounds.Width, bounds.Height) / style.Size, clip);
        var capture = BeginInputSlotCapture();
        capture.SetLayerScene3D(null);
        capture._SetClipRect(coordinates.LocalClip);
        var complete = false;
        try
        {
            _builder = capture;
            TPresentation.Paint(new(this), style, frame, local);
            _builder = parent;
            parent.AppendTransformedWidget(capture, matrix, _displaySize, clip);
            complete = true;
        }
        finally
        {
            _builder = parent;
            EndInputSlotCapture(capture, complete);
        }
    }
}
