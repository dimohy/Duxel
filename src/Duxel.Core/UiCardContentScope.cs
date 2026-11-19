namespace Duxel.Core;

internal readonly record struct UiCardContentInput(float Padding, UiCardOrientation Orientation, bool FillWidth, bool FillHeight,
    UiVector2 MinimumSize = default);
internal readonly record struct UiCardPreviewInput(UiEdgeInsets Bleed, bool FillWidth = false, float MinimumWidth = 0);

public readonly partial struct UiControlContext
{
    internal bool CardHasPreview(UiWidget child)
    {
        var root = child.ResolveLayoutRoot(_ui);
        if (root is UiCardPreviewWidget)
            return true;
        if (root is UiCardContentWidget content)
            foreach (var item in content.Children)
                if (item.ResolveLayoutRoot(_ui) is UiCardPreviewWidget)
                    return true;
        return false;
    }
    internal UiVector2 MeasureCardContentChild(UiWidget child, UiCardPreviewWidget? preview, UiCardPreviewInput input,
        UiBoxConstraints constraints)
    {
        using var scope = new UiImmediateContext.CardPreviewScope(_ui, preview, input);
        return MeasureConstrained(child, constraints);
    }
    internal UiCardPreviewWidget? ResolveCardPreview(UiWidget child) => child.ResolveLayoutRoot(_ui) as UiCardPreviewWidget;
    internal UiWidget? ResolveCardSlot(UiWidget child) => child.ResolveLayoutRoot(_ui);
    internal UiCardContentInput CardContentInput(UiCardContentWidget target) => _ui.GetCardContentInput(target);
    internal UiCardPreviewInput CardPreviewInput(UiCardPreviewWidget target) => _ui.GetCardPreviewInput(target);
    internal UiRect CardContentSurface(UiCardContentWidget target) => _ui.GetCardContentSurface(target);
    internal UiRect CardPreviewSurface(UiCardPreviewWidget target) => _ui.GetCardPreviewSurface(target);
    internal UiContainerLayoutResult MeasureCardBody(UiWidget child, UiCardOptions options, UiCardMetrics metrics, UiBoxConstraints constraints)
    {
        using var scope = new UiImmediateContext.CardBodyScope(_ui, child, options, metrics, constraints, default, render: false);
        var layout = UiBoxContainerLayout.Measure(this, child,
            new(Padding: new(metrics.Padding), Margin: metrics.ShadowInsets, Width: options.Width, Height: options.Height), constraints);
        if (child.ResolveLayoutRoot(_ui) is UiCardPreviewWidget && options.Orientation == UiCardOrientation.Horizontal)
            layout = layout with
            {
                Child = layout.Child with
                {
                    Y = layout.Child.Y + MathF.Max(0, layout.Surface.Height - 2 * metrics.Padding - layout.Child.Height) / 2
                }
            };
        return layout;
    }
    internal UiImmediateContext.CardBodyScope BeginCardBody(UiWidget child, UiCardOptions options, UiCardMetrics metrics, UiRect surface)
        => new(_ui, child, options, metrics, LayoutConstraints, surface, render: true);
}

public sealed partial class UiImmediateContext
{
    private UiCardContentWidget? _cardContentTarget;
    private UiCardContentInput _cardContentInput;
    private UiCardPreviewWidget? _cardPreviewTarget;
    private UiCardPreviewInput _cardPreviewInput;
    private UiRect _cardContentSurface;
    internal UiRect GetCardPreviewSurface(UiCardPreviewWidget target)
        => ReferenceEquals(target, _cardPreviewTarget) ? _cardContentSurface : CurrentClipRect;
    internal UiCardContentInput GetCardContentInput(UiCardContentWidget target)
        => ReferenceEquals(target, _cardContentTarget) ? _cardContentInput
            : throw new InvalidOperationException("CardContent requires a Card primary content slot through composition-only widgets.");
    internal UiRect GetCardContentSurface(UiCardContentWidget target)
        => ReferenceEquals(target, _cardContentTarget) ? _cardContentSurface
            : throw new InvalidOperationException("CardContent requires its Card surface.");
    internal UiCardPreviewInput GetCardPreviewInput(UiCardPreviewWidget target)
        => ReferenceEquals(target, _cardPreviewTarget) ? _cardPreviewInput : default;

    internal readonly struct CardBodyScope : IDisposable
    {
        private readonly UiImmediateContext _owner;
        private readonly UiCardContentWidget? _content;
        private readonly UiCardContentInput _input;
        private readonly UiCardPreviewWidget? _preview;
        private readonly UiCardPreviewInput _previewInput;
        private readonly UiRect _surface;
        internal CardBodyScope(UiImmediateContext ui, UiWidget child, UiCardOptions options, UiCardMetrics metrics,
            UiBoxConstraints constraints, UiRect surface, bool render)
        {
            _owner = ui;
            _content = ui._cardContentTarget;
            _input = ui._cardContentInput;
            _preview = ui._cardPreviewTarget;
            _previewInput = ui._cardPreviewInput;
            _surface = ui._cardContentSurface;
            var root = child.ResolveLayoutRoot(ui);
            ui._cardContentTarget = root as UiCardContentWidget;
            ui._cardPreviewTarget = root as UiCardPreviewWidget;
            // Box containers loosen the child's measuring constraints. Preserve the Card's
            // actual body minimum separately so virtual direct slots still fill that body.
            var box = constraints.Deflate(metrics.ShadowInsets);
            if (options.Width is { } width)
            {
                var value = Math.Clamp(width, box.MinWidth, box.MaxWidth);
                box = box with
                {
                    MinWidth = value,
                    MaxWidth = value
                };
            }
            if (options.Height is { } height)
            {
                var value = Math.Clamp(height, box.MinHeight, box.MaxHeight);
                box = box with
                {
                    MinHeight = value,
                    MaxHeight = value
                };
            }
            var body = box.Deflate(new(metrics.Padding));
            var input = new UiCardContentInput(metrics.Padding, options.Orientation,
                options.Width.HasValue || constraints.MinWidth == constraints.MaxWidth,
                options.Height.HasValue || constraints.MinHeight == constraints.MaxHeight,
                new(body.MinWidth, body.MinHeight));
            ui._cardContentInput = render && root is UiCardContentWidget content ? content.PreparedInput : input;
            ui._cardPreviewInput = render && root is UiCardPreviewWidget preview ? preview.PreparedInput
                : new(new(metrics.Padding), input.Orientation == UiCardOrientation.Vertical && input.FillWidth,
                    input.Orientation == UiCardOrientation.Vertical ? input.MinimumSize.X : 0);
            ui._cardContentSurface = surface;
        }
        public void Dispose()
        {
            _owner._cardContentTarget = _content;
            _owner._cardContentInput = _input;
            _owner._cardPreviewTarget = _preview;
            _owner._cardPreviewInput = _previewInput;
            _owner._cardContentSurface = _surface;
        }
    }
    internal readonly struct CardPreviewScope : IDisposable
    {
        private readonly UiImmediateContext _owner;
        private readonly UiCardPreviewWidget? _target;
        private readonly UiCardPreviewInput _input;
        internal CardPreviewScope(UiImmediateContext ui, UiCardPreviewWidget? target, UiCardPreviewInput input)
        {
            _owner = ui;
            _target = ui._cardPreviewTarget;
            _input = ui._cardPreviewInput;
            ui._cardPreviewTarget = target;
            ui._cardPreviewInput = input;
        }
        public void Dispose()
        {
            _owner._cardPreviewTarget = _target;
            _owner._cardPreviewInput = _input;
        }
    }
    internal void RenderCardContentChild(UiWidget child, UiCardPreviewWidget? preview, UiCardPreviewInput input,
        UiRect bounds, UiRect root)
    {
        using var scope = new CardPreviewScope(this, preview, input);
        RenderParentDataChild(child, null, bounds, root, clipToAllocation: preview is null);
    }
}
