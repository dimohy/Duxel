namespace Duxel.Core;

/// <summary>Card-owned direct content sequence: source gaps, preview bleed, and horizontal header/footer growth.</summary>
public class UiCardContentWidget : UiMultiChildWidget
{
    private UiRect[] _rects;
    private UiVector2[] _sizes;
    private UiCardPreviewWidget?[] _previews;
    private UiCardPreviewInput[] _previewInputs;
    private UiRetainedLayout<Layout>? _retained;
    private UiCardContentInput _preparedInput;
    private bool _hasPreparedInput;
    private readonly record struct Layout(UiVector2 Size, UiCardContentInput Input);
    internal UiCardContentInput PreparedInput => _hasPreparedInput ? _preparedInput
        : throw new InvalidOperationException("CardContent requires its measured parent allocation before rendering.");
    public UiCardContentWidget(UiWidget[] children, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(children, identity)
    {
        _rects = new UiRect[children.Length];
        _sizes = new UiVector2[children.Length];
        _previews = new UiCardPreviewWidget?[children.Length];
        _previewInputs = new UiCardPreviewInput[children.Length];
    }
    protected override bool CanRetainArrangement => true;
    protected override bool CanRetainMeasurement => _retained is { IsValid: true };
    internal override void InvalidateRetainedMeasurement() => _retained?.Invalidate();
    private Layout MeasureLayout(UiControlContext context, bool render = false)
    {
        var input = context.CardContentInput(this);
        var allowed = context.CanRetainLayoutMeasurement;
        if (_retained is { } previous)
        {
            if (previous.IsValid && previous.CachedResult.Input != input)
                RequestRebuild();
            if (previous.TryGet(context, render, out var result, allowed))
                return result;
        }
        var cache = _retained ??= new();
        var attempt = cache.Begin(context);
        var constraints = context.LayoutConstraints.Validate();
        constraints = constraints with
        {
            MinWidth = Math.Clamp(input.MinimumSize.X, constraints.MinWidth, constraints.MaxWidth),
            MinHeight = Math.Clamp(input.MinimumSize.Y, constraints.MinHeight, constraints.MaxHeight)
        };
        var horizontal = input.Orientation == UiCardOrientation.Horizontal;
        var crossMaximum = horizontal ? constraints.MaxHeight : constraints.MaxWidth;
        var cross = 0f;
        UiBoxConstraints ChildConstraints(float crossMinimum, float crossMax, float mainMinimum = 0, float mainMaximum = float.PositiveInfinity)
            => horizontal ? new(mainMinimum, mainMaximum, crossMinimum, crossMax) : new(crossMinimum, crossMax, mainMinimum, mainMaximum);
        for (var index = 0; index < Children.Length; index++)
        {
            var preview = _previews[index] = context.ResolveCardPreview(Children[index]);
            var p = preview is null ? 0 : input.Padding;
            var bleed = horizontal ? new UiEdgeInsets(index == 0 ? p : 0, p, index == Children.Length - 1 ? p : 0, p)
                : new UiEdgeInsets(p, index == 0 ? p : 0, p, index == Children.Length - 1 ? p : 0);
            _previewInputs[index] = new(bleed);
            // Header/Footer stretch to every bounded width they receive. Resolve their
            // natural width before selecting an auto-sized vertical Card's body width.
            var intrinsicSlot = context.ResolveCardSlot(Children[index]) is UiCardHeaderWidget or UiCardFooterWidget;
            var initialCrossMaximum = !horizontal && !input.FillWidth && intrinsicSlot ? float.PositiveInfinity : crossMaximum;
            _sizes[index] = context.MeasureCardContentChild(Children[index], preview, _previewInputs[index], ChildConstraints(0, initialCrossMaximum));
            cross = MathF.Max(cross, horizontal ? _sizes[index].Y : _sizes[index].X);
        }
        var fillCross = horizontal ? input.FillHeight : input.FillWidth;
        if (fillCross && float.IsFinite(crossMaximum))
            cross = crossMaximum;
        if (!horizontal)
        {
            cross = Math.Clamp(cross, constraints.MinWidth, constraints.MaxWidth);
            for (var index = 0; index < Children.Length; index++)
                _sizes[index] = context.MeasureCardContentChild(Children[index], _previews[index], _previewInputs[index], ChildConstraints(cross, cross));
        }
        double TotalMain()
        {
            var total = (double)input.Padding * Math.Max(0, Children.Length - 1);
            foreach (var size in _sizes)
                total += horizontal ? size.X : size.Y;
            return total;
        }
        var main = TotalMain();
        var maximum = horizontal ? constraints.MaxWidth : constraints.MaxHeight;
        if (main > maximum || main > float.MaxValue)
            throw new InvalidOperationException("Card content exceeds its main-axis allocation.");
        var targetMain = input.FillWidth && float.IsFinite(maximum) ? maximum : Math.Max(main, constraints.MinWidth);
        if (horizontal && targetMain > main && Children.Length > 0
            && context.ResolveCardSlot(Children[^1]) is UiCardHeaderWidget or UiCardFooterWidget)
        {
            var index = Children.Length - 1;
            var width = _sizes[index].X + (float)(targetMain - main);
            _sizes[index] = context.MeasureCardContentChild(Children[index], null, default, ChildConstraints(0, crossMaximum, width, width));
            main = TotalMain();
            cross = 0;
            foreach (var size in _sizes)
                cross = MathF.Max(cross, size.Y);
            if (input.FillHeight && float.IsFinite(crossMaximum))
                cross = crossMaximum;
        }
        var sizeResult = constraints.Constrain(horizontal ? new((float)main, cross) : new(cross, (float)main));
        var cursor = 0f;
        for (var index = 0; index < Children.Length; index++)
        {
            var child = _sizes[index];
            _rects[index] = horizontal ? new(cursor, (sizeResult.Y - child.Y) / 2, child.X, child.Y) : new(0, cursor, child.X, child.Y);
            cursor += (horizontal ? child.X : child.Y) + input.Padding;
        }
        _preparedInput = input;
        _hasPreparedInput = true;
        var layout = new Layout(sizeResult, input);
        cache.Commit(context, attempt, layout, sizeResult, allowed && ChildrenRetainMeasurement);
        return layout;
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => MeasureLayout(context).Size;
    protected override void RenderControl(UiImmediateContext ui)
    {
        var context = new UiControlContext(ui);
        var layout = MeasureLayout(context, render: true);
        var origin = ui.AllocateLayoutBox(layout.Size);
        var root = context.CardContentSurface(this);
        for (var index = 0; index < Children.Length; index++)
        {
            var rect = _rects[index] with
            {
                X = origin.X + _rects[index].X,
                Y = origin.Y + _rects[index].Y
            };
            ui.RenderCardContentChild(Children[index], _previews[index], _previewInputs[index], rect, root);
        }
        ui.FinishLayoutBox(origin, layout.Size);
    }
    private protected override void ReleaseOwnedResources()
    {
        _rects = [];
        _sizes = [];
        _previews = [];
        _previewInputs = [];
        _retained = null;
        _hasPreparedInput = false;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiCardContentWidget))]
[UiComponent("CardContent", UiComponentExposure.Infrastructure)]
public partial class UiCardContent;

public static partial class Dux
{
    public static UiCardContent CardContent(params UiWidget[] children) => new(children);
}
