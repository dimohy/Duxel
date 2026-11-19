// Card Header layout follows Fluent UI (MIT, Microsoft Corporation); see Duxel.Core.ThirdPartyNotices.txt.
namespace Duxel.Core;

/// <summary>Owns optional image, title, description and action slots in source order.</summary>
public class UiCardHeaderWidget : UiMultiChildWidget
{
    private readonly int _imageIndex;
    private readonly int _headerIndex;
    private readonly int _descriptionIndex;
    private readonly int _actionIndex;
    private UiRect[] _rects;
    private UiRetainedLayout<UiVector2>? _retained;

    public UiCardHeaderWidget(UiWidget? header = null, UiWidget? description = null,
        UiWidget? image = null, UiWidget? action = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(Combine(image, header, description, action), identity)
    {
        var index = 0;
        _imageIndex = image is null ? -1 : index++;
        _headerIndex = header is null ? -1 : index++;
        _descriptionIndex = description is null ? -1 : index++;
        _actionIndex = action is null ? -1 : index++;
        _rects = new UiRect[index];
    }

    public UiWidget? Image => Slot(_imageIndex);
    public UiWidget? Header => Slot(_headerIndex);
    public UiWidget? Description => Slot(_descriptionIndex);
    public UiWidget? Action => Slot(_actionIndex);
    private UiWidget? Slot(int index) { ThrowIfDisposed(); return index < 0 ? null : Children[index]; }
    protected override bool CanRetainArrangement => true;
    protected override bool CanRetainMeasurement => _retained is { IsValid: true };
    internal override void InvalidateRetainedMeasurement() => _retained?.Invalidate();

    private static UiWidget[] Combine(UiWidget? image, UiWidget? header, UiWidget? description, UiWidget? action)
    {
        var children = new List<UiWidget>(4);
        if (image is not null) children.Add(image);
        if (header is not null) children.Add(header);
        if (description is not null) children.Add(description);
        if (action is not null) children.Add(action);
        return children.ToArray();
    }

    private UiVector2 MeasureLayout(UiControlContext context, bool render = false)
    {
        var allowed = CanRetainArrangement && context.CanRetainLayoutMeasurement;
        if (_retained is { } previous && previous.TryGet(context, render, out var previousSize, allowed))
            return previousSize;
        var cache = _retained ??= new();
        var attempt = cache.Begin(context);
        var constraints = context.LayoutConstraints.Validate();
        UiVector2 Measure(int index, float width) => index < 0 ? default
            : context.MeasureConstrained(Children[index], new(0, width, 0, constraints.MaxHeight));
        var image = Measure(_imageIndex, float.PositiveInfinity);
        var action = Measure(_actionIndex, float.PositiveInfinity);
        var leading = image.X + (_imageIndex < 0 ? 0 : 12);
        var trailing = action.X + (_actionIndex < 0 ? 0 : 12);
        var reserved = (double)leading + trailing;
        if (reserved > constraints.MaxWidth || reserved > float.MaxValue)
            throw new InvalidOperationException("Card Header image, action and margins exceed the available width.");
        var middleAvailable = float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth - (float)reserved : float.PositiveInfinity;
        var header = Measure(_headerIndex, middleAvailable);
        var description = Measure(_descriptionIndex, middleAvailable);
        var naturalMiddle = MathF.Max(header.X, description.X);
        var middle = float.IsFinite(middleAvailable) && (_headerIndex >= 0 || _descriptionIndex >= 0)
            ? middleAvailable : naturalMiddle;
        var middleHeight = (double)header.Y + description.Y;
        var naturalHeight = Math.Max(Math.Max(image.Y, action.Y), middleHeight);
        var naturalWidth = reserved + middle;
        if (naturalHeight > constraints.MaxHeight || naturalHeight > float.MaxValue || naturalWidth > float.MaxValue)
            throw new InvalidOperationException("Card Header slots exceed the available layout capacity.");
        var size = constraints.Constrain(new(float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : (float)naturalWidth,
            (float)naturalHeight));
        if (_headerIndex >= 0 || _descriptionIndex >= 0) middle = size.X - (float)reserved;
        void Place(int index, float x, float y, UiVector2 childSize)
        { if (index >= 0) _rects[index] = new(x, y, childSize.X, childSize.Y); }
        Place(_imageIndex, 0, (size.Y - image.Y) / 2, image);
        Place(_actionIndex, leading + middle + (_actionIndex < 0 ? 0 : 12), (size.Y - action.Y) / 2, action);
        var extra = size.Y - (float)middleHeight;
        if (_headerIndex >= 0)
        {
            header = context.MeasureConstrained(Children[_headerIndex], UiBoxConstraints.Tight(new(middle, header.Y)));
            Place(_headerIndex, leading, _descriptionIndex >= 0 ? extra / 4 : (size.Y - header.Y) / 2, header);
        }
        if (_descriptionIndex >= 0)
        {
            description = context.MeasureConstrained(Children[_descriptionIndex], UiBoxConstraints.Tight(new(middle, description.Y)));
            Place(_descriptionIndex, leading, header.Y + extra * .75f, description);
        }
        cache.Commit(context, attempt, size, size, allowed && ChildrenRetainMeasurement);
        return size;
    }

    protected override UiVector2 MeasureContent(UiControlContext context) => MeasureLayout(context);
    protected override void RenderControl(UiImmediateContext ui)
    {
        var size = MeasureLayout(new(ui), render: true);
        var origin = ui.AllocateLayoutBox(size);
        var root = new UiRect(origin.X, origin.Y, size.X, size.Y);
        for (var i = 0; i < Children.Length; i++)
        {
            var rect = _rects[i];
            ui.RenderLayoutChild(Children[i], rect with { X = origin.X + rect.X, Y = origin.Y + rect.Y }, root);
        }
        ui.FinishLayoutBox(origin, size);
    }
    private protected override void ReleaseOwnedResources()
    {
        _rects = [];
        _retained = null;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiCardHeaderWidget))]
[UiComponent("CardHeader", UiComponentExposure.Fluent)]
public partial class UiFluentCardHeader;
