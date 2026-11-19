namespace Duxel.Core;

public readonly record struct UiCardNudgeOptions(float Width = 360, bool ReducedMotion = false,
    string? CloseLabel = "Dismiss")
{
    public UiCardNudgeOptions() : this(360) { }

    internal void Validate()
    {
        if (!float.IsFinite(Width) || Width <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width));
        if (CloseLabel is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(CloseLabel);
    }
}

/// <summary>An inline suggestion with a controlled open state, an optional close button, and horizontal swipe dismissal.</summary>
public class UiCardNudgeWidget
    : UiSingleChildBox<UiConstrainedBoxLayout, UiBoxConstraints, UiPlainContainerPresentation>
{
    public UiCardNudgeWidget(string title, string description, UiBinding<bool> open,
        UiWidget? action = null, UiCardNudgeOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(CreatePresence(title, description, open, action, options ?? new()),
            new(0, float.PositiveInfinity, 0, float.PositiveInfinity), identity)
        => Open = open;

    public UiBinding<bool> Open { get; }

    private static BoundPresence CreatePresence(string title, string description, UiBinding<bool> open,
        UiWidget? action, UiCardNudgeOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(open);
        options.Validate();
        return new(new Surface(title, description, open, action, options), open, options.ReducedMotion);
    }

    private sealed class BoundPresence(Surface surface, UiBinding<bool> open, bool reducedMotion)
        : UiPresenceGroupWidget([surface], new(ReducedMotion: reducedMotion) { CollapseExitSpace = true })
    {
        // A delegate-backed binding can change without a widget invalidation.
        protected override bool CanRetainMeasurement => false;
        protected override void PrepareChildren(UiControlContext context)
        {
            SetVisible(0, open.Value);
            base.PrepareChildren(context);
        }

        protected override void RenderArrangedChildren(UiImmediateContext ui, ReadOnlySpan<UiRect> layout, UiRect root)
        {
            base.RenderArrangedChildren(ui, layout, root);
            // A close button or action can update the binding after layout in event-wait mode.
            SetVisible(0, open.Value);
        }
    }

    private sealed class Surface : UiCard<UiFluentCardPresentation>
    {
        internal Surface(string title, string description, UiBinding<bool> open, UiWidget? action,
            UiCardNudgeOptions options)
            : this(title, description, action, options, new UiCardSwipeDismissal(open)) { }

        private Surface(string title, string description, UiWidget? action, UiCardNudgeOptions options,
            UiCardSwipeDismissal dismissal)
            : base(title, CreateContent(title, description, action, options.CloseLabel is not null),
                new(Width: options.Width), floatingAction: options.CloseLabel is { } close
                    ? new UiButton<UiReleaseButtonBehavior, UiToastClosePresentation>(close, dismissal.Dismiss) : null)
            => SwipeDismissal = dismissal;

        private static UiWidget CreateContent(string title, string description, UiWidget? action, bool reserveClose)
        {
            UiWidget header = new UiInlineText([new(title, UiInlineTextWeight.Semibold)]);
            if (reserveClose)
                header = Dux.Padding(header, new(0, 0, 24, 0));
            var detail = new UiInlineText([new(description)]);
            var layout = new UiFlexOptions(Spacing: 8, MainAxisSize: UiMainAxisSize.Min);
            return action is null ? Dux.Column(layout, header, detail) : Dux.Column(layout, header, detail, action);
        }
    }
}

[UiWidgetStyle(typeof(UiCardNudgeWidget))]
[UiComponent("CardNudge", UiComponentExposure.Fluent)]
public partial class UiFluentCardNudge;

internal sealed class UiCardSwipeDismissal(UiBinding<bool> open)
{
    internal UiVector2 Origin;
    internal void Dismiss()
    {
        if (open.Value)
            open.Value = false;
    }
}

public sealed partial class UiImmediateContext
{
    private void ProcessCardSwipeDismissal(UiCardSwipeDismissal gesture, UiRect surface, bool enabled, bool childClaimedPointer)
    {
        var id = ResolveId("##card-swipe");
        if (_state.ActiveId == id)
        {
            _ownedPointerControl = id;
            _ownedPointerControlSeen = true;
            if (!enabled || !_leftMouseDown)
                _state.ActiveId = null;
            if (!enabled || !_leftMouseReleased)
                return;
            var deltaX = MathF.Abs(_mousePosition.X - gesture.Origin.X);
            var deltaY = MathF.Abs(_mousePosition.Y - gesture.Origin.Y);
            if (deltaX >= 48 && deltaX > deltaY)
                gesture.Dismiss();
        }
        else if (enabled && !childClaimedPointer && _state.ActiveId is null && _leftMousePressed
            && ItemHoverable(id, surface, setHovered: false))
        {
            gesture.Origin = _mousePosition;
            _state.ActiveId = _ownedPointerControl = id;
            _ownedPointerControlSeen = true;
        }
    }
}
