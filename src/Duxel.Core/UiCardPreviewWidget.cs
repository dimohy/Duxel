// Card Preview constants follow Fluent UI (MIT, Microsoft Corporation); see Duxel.Core.ThirdPartyNotices.txt.
namespace Duxel.Core;

/// <summary>Owns optional body-filling content and a fixed32px logo at bottom/left12px. Logo does not affect intrinsic size.</summary>
public class UiCardPreviewWidget : UiMultiChildWidget
{
    private readonly int _contentIndex, _logoIndex;
    private UiRetainedLayout<Layout>? _retained;
    private UiRetainedLayout<UiVector2>? _logoMeasurement;
    private UiCardPreviewInput _preparedInput;
    private bool _hasPreparedInput;
    private readonly record struct Layout(UiVector2 Size, UiVector2 VisualSize, UiCardPreviewInput Input);
    internal UiCardPreviewInput PreparedInput => _hasPreparedInput ? _preparedInput
        : throw new InvalidOperationException("CardPreview requires its measured parent allocation before rendering.");
    public UiWidget? Content
    {
        get
        {
            ThrowIfDisposed();
            return _contentIndex < 0 ? null : Children[_contentIndex];
        }
    }
    public UiWidget? Logo
    {
        get
        {
            ThrowIfDisposed();
            return _logoIndex < 0 ? null : Children[_logoIndex];
        }
    }
    public UiCardPreviewWidget(UiWidget? content = null, UiWidget? logo = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(Slots(content, logo), identity)
    {
        _contentIndex = content is null ? -1 : 0;
        _logoIndex = logo is null ? -1 : content is null ? 0 : 1;
    }
    private static UiWidget[] Slots(UiWidget? content, UiWidget? logo)
        => content is null ? logo is null ? [] : [logo] : logo is null ? [content] : [content, logo];
    protected override bool CanRetainArrangement => true;
    protected override bool CanRetainMeasurement => _retained is { IsValid: true };
    internal override void InvalidateRetainedMeasurement()
    {
        _retained?.Invalidate();
        _logoMeasurement?.Invalidate();
    }
    private Layout MeasureLayout(UiControlContext context, bool render = false)
    {
        var input = context.CardPreviewInput(this);
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
            MinWidth = Math.Clamp(input.MinimumWidth, constraints.MinWidth, constraints.MaxWidth)
        };
        var bleed = input.Bleed;
        var extraWidth = bleed.Left + bleed.Right;
        var extraHeight = bleed.Top + bleed.Bottom;
        var width = constraints.MaxWidth + extraWidth;
        if (input.FillWidth && !float.IsFinite(width))
            throw new InvalidOperationException("A full-width CardPreview requires bounded width.");
        var visualConstraints = new UiBoxConstraints(
            input.FillWidth ? width : constraints.MinWidth > 0 || constraints.MinWidth == constraints.MaxWidth
                ? constraints.MinWidth + extraWidth : 0,
            width,
            constraints.MinHeight > 0 || constraints.MinHeight == constraints.MaxHeight ? constraints.MinHeight + extraHeight : 0,
            constraints.MaxHeight + extraHeight).Validate();
        var visual = Content is { } content ? context.MeasureConstrained(content, visualConstraints) : visualConstraints.Constrain(default);
        var size = constraints.Constrain(new(MathF.Max(0, visual.X - extraWidth), MathF.Max(0, visual.Y - extraHeight)));
        var layout = new Layout(size, visual, input);
        _preparedInput = input;
        _hasPreparedInput = true;
        cache.Commit(context, attempt, layout, size, allowed && (Content is null || Content.RetainsMeasurement));
        return layout;
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => MeasureLayout(context).Size;
    protected override void RenderControl(UiImmediateContext ui)
    {
        var context = new UiControlContext(ui);
        var layout = MeasureLayout(context, render: true);
        var size = context.ConstrainToLayout(layout.Size);
        var origin = ui.AllocateLayoutBox(size);
        var bleed = layout.Input.Bleed;
        var visual = new UiRect(origin.X - bleed.Left, origin.Y - bleed.Top,
            layout.VisualSize.X + size.X - layout.Size.X, layout.VisualSize.Y + size.Y - layout.Size.Y);
        var root = context.CardPreviewSurface(this);
        var logoBounds = new UiRect(visual.X + 12, visual.Y + visual.Height - 44, 32, 32);
        if (Content is { } content)
            ui.RenderParentDataChild(content, null, visual, root,
                blockPointer: Logo is not null && ui.IsCardPreviewPointerInside(logoBounds, root));
        if (Logo is { } logo)
        {
            _ = MeasureOwnedChild(this, logo, context, UiBoxConstraints.Tight(new(32, 32)), ref _logoMeasurement, render: true);
            ui.RenderParentDataChild(logo, null, logoBounds, root);
        }
        ui.FinishLayoutBox(origin, size);
    }
    private protected override void ReleaseOwnedResources()
    {
        _retained = null;
        _logoMeasurement = null;
        _hasPreparedInput = false;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiCardPreviewWidget))]
[UiComponent("CardPreview", UiComponentExposure.Fluent)]
public partial class UiFluentCardPreview;

public sealed partial class UiImmediateContext
{
    internal bool IsCardPreviewPointerInside(UiRect bounds, UiRect root) => IsLayoutPointerInside(bounds, root);
}
