namespace Duxel.Core;

/// <summary>Independent primary and optional dismissal actions share Tag content, ownership and presentation.</summary>
public class UiInteractionTag<TPrimaryBehavior, TDismissBehavior, TPresentation> : UiTag<TPresentation>
    where TPrimaryBehavior : IUiButtonBehavior
    where TDismissBehavior : IUiButtonBehavior
    where TPresentation : IUiTagPresentation
{
    private Action? _activated;
    private UiBinding<bool>? _selected;
    /// <summary>Optional controlled primary toggle. The getter is authoritative after each requested change.</summary>
    public UiBinding<bool>? Selected
    {
        get => _selected;
        set
        {
            ThrowIfDisposed();
            if (ReferenceEquals(_selected, value))
                return;
            _selected = value;
            RequestRebuild();
        }
    }
    public UiInteractionTag(UiTagContent content, Action activated, Action? dismissed = null, UiTagOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(content, dismissed, options, identity)
    {
        ArgumentNullException.ThrowIfNull(activated);
        if (!TPresentation.SupportsInteraction)
            throw new NotSupportedException("The selected presentation does not support independent tag actions.");
        _activated = activated;
    }
    public UiInteractionTag(string text, Action activated, Action? dismissed = null, UiTagOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : this(new UiTagContent(text), activated, dismissed, options, identity) { }
    protected override UiVector2 MeasureContent(UiControlContext context)
        => context.LayoutConstraints.Constrain(TPresentation.MeasureInteraction(context, Content,
            Options.WithInheritedDirection(context.RightToLeft), IsDismissible));
    protected override void RenderControl(UiImmediateContext ui)
    {
        var result = ui.RenderInteractionTag<TPrimaryBehavior, TDismissBehavior, TPresentation>(ItemId ?? PrimaryInputId(Content.Text), BoundContent,
            Options.WithInheritedDirection(ui.RightToLeft), IsDismissible, ItemEnabled, _selected);
        if (result.Primary)
            _activated?.Invoke();
        if (result.Dismissed)
            OnDismissed();
    }
    private protected override void ReleaseOwnedResources()
    {
        _activated = null;
        _selected = null;
        base.ReleaseOwnedResources();
    }
}

/// <summary>An interaction tag that activates either action on pointer release.</summary>
public class UiInteractionTag<TPresentation> : UiInteractionTag<UiReleaseButtonBehavior, UiReleaseButtonBehavior, TPresentation>
    where TPresentation : IUiTagPresentation
{
    public UiInteractionTag(UiTagContent content, Action activated, Action? dismissed = null, UiTagOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(content, activated, dismissed, options, identity) { }

    public UiInteractionTag(string text, Action activated, Action? dismissed = null, UiTagOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(text, activated, dismissed, options, identity) { }
}

[UiWidgetStyle(typeof(UiInteractionTag<>), typeof(UiFluentTagPresentation))]
[UiComponent("InteractionTag", UiComponentExposure.Fluent)]
public partial class UiFluentInteractionTag;
