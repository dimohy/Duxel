namespace Duxel.Core;

internal readonly struct UiSurfaceTitlePresentation<TPresentation> : IUiTextPresentation where TPresentation : IUiSurfacePresentation
{
    public static bool CanRetainMeasurement => true;
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, string text) => TPresentation.MeasureTitle(context, text);
    public static void Render(UiControlContext context, string text, UiColor? color) => TPresentation.PaintTitle(context, text, color);
}

internal sealed class UiSurfaceClose<TPresentation>(Action close) : UiControlWidget where TPresentation : IUiSurfacePresentation
{
    internal string Label = "Close";
    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(new(24, 24));
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderSurfaceClose<TPresentation>(PrimaryInputId(Label), Label, close);
}

internal sealed class UiSurfaceActionBar : UiMultiChildWidget
{
    internal bool Narrow, RightToLeft, AutoStack;
    internal bool IsStacked { get; private set; }
    internal float Gap;
    private readonly UiVector2[] _sizes;
    internal UiSurfaceActionBar(UiWidget[] actions) : base(actions) => _sizes = new UiVector2[actions.Length];
    internal UiVector2 Layout(UiControlContext context)
    {
        var width = context.LayoutConstraints.MaxWidth;
        if (!float.IsFinite(width))
            throw new InvalidOperationException("Dialog actions require a finite width.");
        var vertical = Narrow;
        var main = 0f;
        var cross = 0f;
        MeasureChildren();
        if (AutoStack && !vertical && main > width)
        {
            vertical = true;
            MeasureChildren();
        }
        IsStacked = vertical;
        return vertical ? new(width, main) : new(MathF.Min(width, main), cross);
        void MeasureChildren()
        {
            main = cross = 0;
            for (var i = 0; i < Children.Length; i++)
            {
                var child = context.MeasureParentDataChild(Children[i], null, vertical
                    ? new(width, width, 0, float.PositiveInfinity) : new(0, width, 0, float.PositiveInfinity));
                _sizes[i] = child;
                main += vertical ? child.Y : child.X;
                cross = MathF.Max(cross, vertical ? child.X : child.Y);
            }
            main += Math.Max(0, Children.Length - 1) * Gap;
        }
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(Layout(context));
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderSurfaceActions(this, _sizes);
}

internal readonly record struct UiSurfaceBodyLayout(UiVector2 Size, UiVector2 Title, float HeaderHeight, float ContentHeight, UiVector2 Actions);

internal sealed class UiSurfaceBody<TPresentation> : UiControlWidget where TPresentation : IUiSurfacePresentation
{
    internal readonly UiText<UiSurfaceTitlePresentation<TPresentation>> Title;
    internal readonly UiSurfaceContent Content;
    internal readonly UiSurfaceActionBar Actions;
    internal readonly UiSurfaceClose<TPresentation> Close;
    internal bool HasClose, FillHeight, RightToLeft, ActionsStart;
    internal float MaximumHeight = float.PositiveInfinity;

    internal UiSurfaceBody(string title, UiWidget content, UiWidget[] actions, Action close)
    {
        Title = new(title);
        Content = new(content);
        Actions = new(actions);
        Close = new(close);
        OwnChild("title", Title);
        OwnChild("content", Content);
        OwnChild("actions", Actions);
        OwnChild("close", Close);
    }
    internal void Configure(bool narrow, bool shortScreen, bool close, string closeLabel, float maximumHeight, bool fillHeight = false, bool rightToLeft = false, bool actionsStart = false, bool scrollBody = true, bool autoStackActions = false)
    {
        var gap = TPresentation.Metrics.Gap;
        var height = shortScreen ? float.PositiveInfinity : maximumHeight;
        if (Actions.Narrow == narrow && Actions.Gap == gap && HasClose == close && Close.Label == closeLabel && MaximumHeight == height
            && FillHeight == fillHeight && RightToLeft == rightToLeft && ActionsStart == actionsStart && Content.Stretch == !scrollBody && Actions.AutoStack == autoStackActions)
            return;
        Actions.Narrow = narrow;
        Actions.AutoStack = autoStackActions;
        Actions.Gap = gap;
        HasClose = close;
        Close.Label = closeLabel;
        MaximumHeight = height;
        FillHeight = fillHeight;
        RightToLeft = rightToLeft;
        ActionsStart = actionsStart;
        Actions.RightToLeft = rightToLeft;
        Content.SetStretch(!scrollBody);
        Actions.RequestRebuild();
        RequestRebuild();
    }
    internal UiSurfaceBodyLayout Layout(UiControlContext context)
    {
        var metrics = TPresentation.Metrics;
        var width = context.LayoutConstraints.MaxWidth;
        if (!float.IsFinite(width))
            throw new InvalidOperationException("Dialog body requires a finite width.");
        var titleWidth = MathF.Max(0, width - (HasClose ? 24 + metrics.Gap : 0));
        var title = context.MeasureParentDataChild(Title, null, new(titleWidth, titleWidth, 0, float.PositiveInfinity));
        if (HasClose)
            context.MeasureParentDataChild(Close, null, UiBoxConstraints.Tight(new(24, 24)));
        var header = MathF.Max(title.Y, HasClose ? 24 : 0);
        var actions = context.MeasureParentDataChild(Actions, null, new(0, width, 0, float.PositiveInfinity));
        var natural = Content.Stretch ? default : context.MeasureParentDataChild(Content.Child, null, new(width, width, 0, float.PositiveInfinity));
        var gaps = metrics.Gap * (Actions.Children.Length == 0 ? 1 : 2);
        var heightLimit = MathF.Min(MaximumHeight, context.LayoutConstraints.MaxHeight);
        var availableHeight = MathF.Max(0, heightLimit - header - actions.Y - gaps);
        if (Content.Stretch && !float.IsFinite(availableHeight)) throw new InvalidOperationException("A non-scrolling surface body requires a finite height.");
        var preferredHeight = MathF.Max(metrics.MinimumContentHeight, natural.Y);
        // Use the same sum as the measured body to keep padding arithmetic from creating subpixel overflow.
        var fits = header + gaps + preferredHeight + actions.Y <= heightLimit;
        var contentHeight = FillHeight || Content.Stretch ? availableHeight
            : fits ? preferredHeight : MathF.Min(preferredHeight, availableHeight);
        context.MeasureParentDataChild(Content, null, UiBoxConstraints.Tight(new(width, contentHeight)));
        // Filling the available space must report the exact limit. Adding the pieces back can round above it.
        var bodyHeight = contentHeight == availableHeight
            ? MathF.Max(heightLimit, header + gaps + actions.Y)
            : header + gaps + contentHeight + actions.Y;
        return new(new(width, bodyHeight), title, header, contentHeight, actions);
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(Layout(context).Size);
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderSurfaceBody(this);
}
