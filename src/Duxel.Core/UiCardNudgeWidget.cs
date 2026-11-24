namespace Duxel.Core;

public readonly record struct UiCardNudgeOptions(float Width = 360, bool ReducedMotion = false,
    string? CloseLabel = "Dismiss")
{
    public UiCardNudgeOptions() : this(360) { }

    public UiMenuIcon LeadingIcon { get; init; }
    public UiMenuIcon AccentIcon { get; init; }
    public string? AccentText { get; init; }
    /// <summary>Invoked from the message area independently of the action widget and close button.</summary>
    public Action? MessageAction { get; init; }

    internal void Validate()
    {
        if (!float.IsFinite(Width) || Width <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width));
        if (CloseLabel is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(CloseLabel);
        if (AccentText is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(AccentText);
    }
}

/// <summary>Chooses close button input and recognizes completed swipes that began on the card surface.</summary>
public interface IUiCardNudgeBehavior : IUiButtonBehavior
{
    static abstract bool ShouldDismiss(UiVector2 displacement);
}

public readonly struct UiDefaultCardNudgeBehavior : IUiCardNudgeBehavior
{
    public static bool AccessibilityFocus => true;
    public static UiButtonInteraction Process(UiControlContext context, string id, UiRect bounds) =>
        UiReleaseButtonBehavior.Process(context, id, bounds);

    public static bool ShouldDismiss(UiVector2 displacement)
    {
        var horizontal = MathF.Abs(displacement.X);
        return horizontal >= 48 && horizontal > MathF.Abs(displacement.Y);
    }
}

/// <summary>An inline suggestion with a controlled open state, an optional close button, and swipe dismissal.</summary>
public class UiCardNudgeWidget<TBehavior, TPresentation>
    : UiSingleChildBox<UiConstrainedBoxLayout, UiBoxConstraints, UiPlainContainerPresentation>
    where TBehavior : IUiCardNudgeBehavior
    where TPresentation : IUiCardPresentation
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

    private sealed class Surface : UiCard<TPresentation>
    {
        internal Surface(string title, string description, UiBinding<bool> open, UiWidget? action,
            UiCardNudgeOptions options)
            : this(title, description, action, options, new UiCardSwipeDismissal(open, TBehavior.ShouldDismiss)) { }

        private Surface(string title, string description, UiWidget? action, UiCardNudgeOptions options,
            UiCardSwipeDismissal dismissal)
            : base(title, CreateContent(title, description, action, options, dismissal),
                new(Width: options.Width), floatingAction: options.CloseLabel is { } close
                    ? new UiButton<TBehavior, UiToastClosePresentation>(close, dismissal.Dismiss) : null)
            => SwipeDismissal = dismissal;

        private static UiWidget CreateContent(string title, string description, UiWidget? action,
            UiCardNudgeOptions options, UiCardSwipeDismissal dismissal)
        {
            UiWidget header = new UiInlineText([new(title, UiInlineTextWeight.Semibold)]);
            if (options.CloseLabel is not null)
                header = Dux.Padding(header, new(0, 0, 24, 0));
            var detail = new UiInlineText([new(description)]);
            var layout = new UiFlexOptions(Spacing : 8, MainAxisSize : UiMainAxisSize.Min);
            if (options.LeadingIcon.IsEmpty && options.AccentIcon.IsEmpty && options.AccentText is null && options.MessageAction is null)
                return action is null ? Dux.Column(layout, header, detail) : Dux.Column(layout, header, detail, action);

            var messageParts = new List<UiWidget> { header, detail };
            if (options.AccentText is { } accent)
            {
                var text = new UiInlineText([new(accent, UiInlineTextWeight.Semibold)]);
                messageParts.Add(options.AccentIcon.IsEmpty ? text : Dux.Row(
                    new(Spacing : 4, MainAxisSize : UiMainAxisSize.Min, CrossAxisAlignment : UiCrossAxisAlignment.Center),
                    new NudgeIcon(options.AccentIcon, 12), Dux.Flexible(text)));
            }
            else if (!options.AccentIcon.IsEmpty)
                messageParts.Add(new NudgeIcon(options.AccentIcon, 12));

            UiWidget message = Dux.Column(layout, [.. messageParts]);
            if (!options.LeadingIcon.IsEmpty)
                message = Dux.Row(layout, new NudgeIcon(options.LeadingIcon, 24), Dux.Flexible(message));
            if (options.MessageAction is { } invoke)
                message = new Message(message, title, description, invoke, dismissal);
            return action is null ? message : Dux.Column(layout, message, action);
        }
    }

    private sealed class NudgeIcon(UiMenuIcon icon, float edge) : UiControlWidget
    {
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(new(edge, edge));
        protected override void RenderControl(UiImmediateContext ui) => ui.RenderIcon(icon, default, new(edge, edge), ui.GetColorU32(UiStyleColor.Text));
    }

    private sealed class Message(UiWidget content, string title, string description, Action invoke, UiCardSwipeDismissal dismissal)
        : UiSingleChildBox<UiPaddingLayout, UiEdgeInsets, UiPlainContainerPresentation>(content, default)
    {
        protected override void RenderControl(UiImmediateContext ui)
        {
            if (ui.RenderCardNudgeMessage(Child!, GetLayout(new(ui), render : true), title, description, dismissal))
                invoke();
        }
    }
}

/// <summary>Fluent CardNudge with default close and swipe behavior.</summary>
public class UiCardNudgeWidget : UiCardNudgeWidget<UiDefaultCardNudgeBehavior, UiFluentCardPresentation>
{
    public UiCardNudgeWidget(string title, string description, UiBinding<bool> open,
        UiWidget? action = null, UiCardNudgeOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance)
        : base(title, description, open, action, options, identity) { }
}

[UiWidgetStyle(typeof(UiCardNudgeWidget))]
[UiComponent("CardNudge", UiComponentExposure.Fluent)]
public partial class UiFluentCardNudge;

internal sealed class UiCardSwipeDismissal(UiBinding<bool> open, Func<UiVector2, bool> shouldDismiss)
{
    internal UiVector2 Origin;
    internal bool ShouldDismiss(UiVector2 displacement) => shouldDismiss(displacement);
    internal void Dismiss()
    {
        if (open.Value)
            open.Value = false;
    }
}

public sealed partial class UiImmediateContext
{
    internal bool RenderCardNudgeMessage(UiWidget child, UiContainerLayoutResult layout, string title, string description,
        UiCardSwipeDismissal dismissal)
    {
        var origin = AllocateLayoutBox(layout.Size);
        var bounds = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        using var clip = BeginControlClip(bounds);
        const string id = "##nudge-message";
        var wasPressed = _state.ActiveId == ResolveId(id);
        var context = new UiControlContext(this);
        var interaction = ProcessPlacedButton<UiReleaseButtonBehavior>(context, id, bounds, out var resolved);
        if (_leftMousePressed && _state.ActiveId == resolved)
            dismissal.Origin = _mousePosition;

        var activated = interaction.Activated;
        if (wasPressed && _leftMouseReleased && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier
            && dismissal.ShouldDismiss(new(_mousePosition.X - dismissal.Origin.X, _mousePosition.Y - dismissal.Origin.Y)))
        {
            dismissal.Dismiss();
            activated = false;
        }

        RenderLayoutChild(child, layout.Child with { X = origin.X + layout.Child.X, Y = origin.Y + layout.Child.Y }, bounds);
        if (interaction.FocusVisible)
            context.FocusOutline(bounds, 4);
        PublishPlacedButton<UiReleaseButtonBehavior>(resolved, title, bounds, description);
        FinishLayoutBox(origin, layout.Size);
        return activated;
    }

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
            if (gesture.ShouldDismiss(new(_mousePosition.X - gesture.Origin.X, _mousePosition.Y - gesture.Origin.Y)))
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
