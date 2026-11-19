// MessageBar layout follows Fluent UI (MIT, Microsoft Corporation); see Duxel.Core.ThirdPartyNotices.txt.
namespace Duxel.Core;

public enum UiMessageBarIntent
{
    Info, Success, Warning, Error
}
public enum UiMessageBarLayoutMode
{
    Auto, SingleLine, MultiLine
}
public enum UiMessageBarShape
{
    Rounded, Square
}
public readonly record struct UiMessageBarOptions(UiMessageBarIntent Intent = UiMessageBarIntent.Info,
    UiMessageBarLayoutMode Layout = UiMessageBarLayoutMode.Auto, UiMessageBarShape Shape = UiMessageBarShape.Rounded,
    float? Width = null, bool HideIcon = false, bool RightToLeft = false)
{
    public UiAnnouncementPriority? AnnouncementPriority
    {
        get; init;
    }
    public bool DisableAnnouncement
    {
        get; init;
    }
    public string? AnnouncementText
    {
        get; init;
    }
    public void Validate()
    {
        if ((uint)Intent > 3 || (uint)Layout > 2 || (uint)Shape > 1)
            throw new ArgumentOutOfRangeException(nameof(UiMessageBarOptions));
        new UiContainerOptions(Width: Width).Validate();
        if (AnnouncementPriority is { } priority && priority is not (UiAnnouncementPriority.Polite or UiAnnouncementPriority.Assertive))
            throw new ArgumentOutOfRangeException(nameof(AnnouncementPriority));
        if (AnnouncementText is { } text)
            new UiAccessibilityAnnouncement(text, "message-bar").Validate();
    }
}

public readonly record struct UiMessageBarMetrics(float Border = 1, float Padding = 12, float IconEdge = 20,
    float IconGap = 8, float MinimumHeight = 36, float MultilineTop = 10, float MultilineBottom = 8,
    float ActionRowGap = 10, float CornerRadius = 4)
{
    internal UiMessageBarMetrics Validate()
    {
        ReadOnlySpan<float> values = stackalloc float[] { Border, Padding, IconEdge, IconGap, MinimumHeight, MultilineTop,
            MultilineBottom, ActionRowGap, CornerRadius };
        foreach (var value in values)
            if (!float.IsFinite(value) || value < 0)
                throw new InvalidOperationException("MessageBar presentation metrics must be finite and nonnegative.");
        return this;
    }
}

public readonly record struct UiMessageBarLayout(UiVector2 Size, UiRect Body, UiRect Icon,
    UiRect Actions, UiRect ContainerAction, bool Multiline);

public interface IUiMessageBarPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiMessageBarMetrics Metrics
    {
        get;
    }
    static abstract float SingleLineHeight(UiControlContext context);
    static abstract void Paint(UiControlContext context, UiRect bounds, UiRect icon, UiMessageBarOptions options);
    static virtual UiColor? Foreground(UiControlContext context, UiMessageBarOptions options) => null;
}

/// <summary>Owns the body, secondary actions and container action in source navigation order.</summary>
public class UiMessageBar<TPresentation> : UiMultiChildWidget, IUiMessageBarContent where TPresentation : IUiMessageBarPresentation
{
    private readonly int _actionsIndex;
    private readonly int _containerActionIndex;
    private string _label;
    private UiMessageBarOptions _options;
    private UiRetainedLayout<UiMessageBarLayout>? _retained;
    internal UiMessageBarLayout LastLayout
    {
        get; private set;
    }

    public UiMessageBar(string label, UiWidget body, UiWidget? actions = null, UiWidget? containerAction = null,
        UiMessageBarOptions options = default, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(Combine(label, body, actions, containerAction, options), identity)
    {
        _label = label;
        _options = options;
        _actionsIndex = actions is null ? -1 : 1;
        _containerActionIndex = containerAction is null ? -1 : actions is null ? 1 : 2;
        ApplyChildDirection(options.RightToLeft);
    }

    public UiWidget Body
    {
        get
        {
            ThrowIfDisposed();
            return Children[0];
        }
    }
    public UiWidget? Actions
    {
        get
        {
            ThrowIfDisposed();
            return _actionsIndex < 0 ? null : Children[_actionsIndex];
        }
    }
    public UiWidget? ContainerAction
    {
        get
        {
            ThrowIfDisposed();
            return _containerActionIndex < 0 ? null : Children[_containerActionIndex];
        }
    }
    public string Label
    {
        get => _label;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            if (_label == value)
                return;
            ThrowIfBuilding();
            _label = value;
            RequestRebuild();
        }
    }
    public UiMessageBarOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            ThrowIfBuilding();
            ApplyChildDirection(value.RightToLeft);
            _options = value;
            RequestRebuild();
        }
    }

    private void ApplyChildDirection(bool rightToLeft)
    {
        if (Body is UiMessageBarBodyWidget body)
        {
            body.HorizontalAlignment = rightToLeft ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left;
            body.RightToLeft = rightToLeft;
        }
        if (Actions is UiMessageBarActionsWidget actions)
            actions.Options = actions.Options with
            {
                RightToLeft = rightToLeft
            };
    }

    protected override bool CanRetainArrangement => TPresentation.CanRetainMeasurement;
    protected override bool CanRetainMeasurement => _retained is { IsValid: true };
    internal override void InvalidateRetainedMeasurement() => _retained?.Invalidate();

    private static UiWidget[] Combine(string label, UiWidget body, UiWidget? actions, UiWidget? containerAction,
        UiMessageBarOptions options)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(body);
        options.Validate();
        var children = new List<UiWidget>(3) { body };
        if (actions is not null)
            children.Add(actions);
        if (containerAction is not null)
            children.Add(containerAction);
        return children.ToArray();
    }

    private UiMessageBarLayout MeasureLayout(UiControlContext context, bool render = false)
    {
        var allowed = CanRetainArrangement && context.CanRetainLayoutMeasurement;
        if (_retained is { } previous && previous.TryGet(context, render, out var found, allowed))
            return LastLayout = found;
        var cache = _retained ??= new();
        var attempt = cache.Begin(context);
        var metrics = TPresentation.Metrics.Validate();
        var singleLineHeight = TPresentation.SingleLineHeight(context);
        if (!float.IsFinite(singleLineHeight) || singleLineHeight <= 0)
            throw new InvalidOperationException("MessageBar line height must be finite and positive.");
        var constraints = context.LayoutConstraints.Validate();
        UiVector2 Measure(UiWidget? child, float width) => child is null ? default
            : context.MeasureConstrained(child, new(0, width, 0, float.PositiveInfinity));
        var bodyNatural = Measure(Body, float.PositiveInfinity);
        var actionsNatural = Measure(Actions, float.PositiveInfinity);
        var hasActions = Actions is not null && actionsNatural != default;
        var container = Measure(ContainerAction, float.PositiveInfinity);
        var iconWidth = _options.HideIcon ? 0 : metrics.IconEdge + metrics.IconGap;
        var containerWidth = ContainerAction is null ? 0 : container.X + metrics.Padding;
        var actionsWidth = !hasActions ? 0 : actionsNatural.X + metrics.Padding;
        var fixedWidth = 2d * metrics.Border + metrics.Padding + iconWidth + metrics.Padding + containerWidth;
        var desiredWidth = UiLayoutDistribution.Finite(fixedWidth + bodyNatural.X + actionsWidth);
        var width = Math.Clamp(_options.Width ?? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : desiredWidth),
            constraints.MinWidth, constraints.MaxWidth);
        var multiline = _options.Layout == UiMessageBarLayoutMode.MultiLine
            || _options.Layout == UiMessageBarLayoutMode.Auto && (desiredWidth > width || bodyNatural.Y > singleLineHeight);
        if (_options.Layout == UiMessageBarLayoutMode.SingleLine && desiredWidth > width)
            throw new InvalidOperationException("Forced single-line MessageBar content exceeds its allocation; use automatic or multiline layout.");
        var bodyWidth = (double)width - fixedWidth - (multiline ? 0 : actionsWidth);
        if (bodyWidth < 0 || bodyWidth > float.MaxValue)
            throw new InvalidOperationException("MessageBar icon, container action and padding exceed the available width.");
        var body = Measure(Body, (float)bodyWidth);
        if (body.X > bodyWidth)
            throw new InvalidOperationException("MessageBar body must honor its available width.");
        var actions = actionsNatural;
        if (multiline && hasActions)
        {
            var actionWidth = (double)width - 2 * metrics.Border - 2 * metrics.Padding;
            if (actionWidth < 0)
                throw new InvalidOperationException("MessageBar action padding exceeds the available width.");
            actions = Measure(Actions, (float)actionWidth);
            if (actions.X > actionWidth)
                throw new InvalidOperationException("MessageBar actions must wrap or honor their available width.");
        }
        var iconHeight = _options.HideIcon ? 0 : metrics.IconEdge;
        var rowHeight = MathF.Max(body.Y, MathF.Max(iconHeight, container.Y));
        var contentHeight = multiline
            ? 2d * metrics.Border + metrics.MultilineTop + rowHeight + metrics.MultilineBottom
                + (!hasActions ? 0 : metrics.ActionRowGap + actions.Y)
            : 2d * metrics.Border + MathF.Max(rowHeight, actions.Y);
        var height = UiLayoutDistribution.Finite(Math.Max(metrics.MinimumHeight, contentHeight));
        if (height > constraints.MaxHeight)
            throw new InvalidOperationException($"MessageBar '{_label}' content height {height:R} exceeds allocation {constraints.MaxHeight:R}; width {width:R}, body {body}, container action {container}, previous layout {LastLayout}.");
        var size = constraints.Constrain(new(width, height));
        var x = metrics.Border + metrics.Padding;
        var rowY = multiline ? metrics.Border + metrics.MultilineTop : metrics.Border;
        var availableHeight = multiline ? rowHeight : size.Y - 2 * metrics.Border;
        UiRect Place(float left, UiVector2 extent) => new(left,
            rowY + (multiline ? 0 : (availableHeight - extent.Y) / 2), extent.X, extent.Y);
        var iconRect = Place(x, _options.HideIcon ? default : new(metrics.IconEdge, metrics.IconEdge));
        var bodyRect = Place(x + iconWidth, new((float)bodyWidth, body.Y));
        var containerRect = Place(size.X - metrics.Border - metrics.Padding - container.X, container);
        var actionsRect = !hasActions ? default : multiline
            ? new(size.X - metrics.Border - metrics.Padding - actions.X,
                rowY + rowHeight + metrics.ActionRowGap, actions.X, actions.Y)
            : Place(size.X - metrics.Border - containerWidth - metrics.Padding - actions.X, actions);
        UiRect Mirror(UiRect rect) => _options.RightToLeft ? rect with
        {
            X = size.X - rect.X - rect.Width
        } : rect;
        var layout = new UiMessageBarLayout(size, Mirror(bodyRect), Mirror(iconRect), Mirror(actionsRect), Mirror(containerRect), multiline);
        cache.Commit(context, attempt, layout, size, allowed && ChildrenRetainMeasurement);
        return LastLayout = layout;
    }

    protected override UiVector2 MeasureContent(UiControlContext context) => MeasureLayout(context).Size;
    protected override void RenderControl(UiImmediateContext ui)
        => ui.RenderMessageBar<TPresentation>(_label, Body, Actions, ContainerAction, _options, MeasureLayout(new(ui), render: true));

    private protected override void ReleaseOwnedResources()
    {
        _retained = null;
        _label = string.Empty;
        base.ReleaseOwnedResources();
    }
}

[UiWidgetStyle(typeof(UiMessageBar<>), typeof(UiFluentMessageBarPresentation))]
[UiComponent("MessageBar", UiComponentExposure.Fluent)]
public partial class UiFluentMessageBar;
