namespace Duxel.Core;

public enum UiTagSize
{
    Medium, Small, ExtraSmall
}
public enum UiTagAppearance
{
    Filled, Outline, Brand
}
public enum UiTagShape
{
    Rounded, Circular
}

public readonly record struct UiTagOptions(UiTagSize Size = UiTagSize.Medium,
    UiTagAppearance Appearance = UiTagAppearance.Filled, UiTagShape Shape = UiTagShape.Rounded,
    bool RightToLeft = false, string DismissLabel = "Remove", bool Selected = false)
{
    public UiTagOptions() : this(UiTagSize.Medium, UiTagAppearance.Filled, UiTagShape.Rounded, false, "Remove") { }
    internal UiTagOptions WithInheritedDirection(bool rightToLeft) => rightToLeft
        ? this with { RightToLeft = true } : this;
    internal void Validate()
    {
        if ((uint)Size > 2 || (uint)Appearance > 2 || (uint)Shape > 1)
            throw new ArgumentOutOfRangeException(nameof(UiTagOptions));
        ArgumentException.ThrowIfNullOrWhiteSpace(DismissLabel);
        if (UiAccessibilityTree.ContainsControl(DismissLabel, false))
            throw new ArgumentException("Dismiss labels must be single-line.", nameof(DismissLabel));
    }
    internal float Spacing => Size == UiTagSize.Medium ? 8 : Size == UiTagSize.Small ? 6 : 4;
}

public readonly record struct UiTagContent(string Text, string? SecondaryText = null, UiMenuIcon Icon = default, UiTagMedia Media = default);

public interface IUiTagPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static virtual bool SupportsInteraction => false;
    static abstract UiVector2 Measure(UiControlContext context, UiTagContent content, UiTagOptions options, bool dismissible);
    static abstract void Paint(UiControlContext context, UiTagContent content, UiRect bounds,
        UiTagOptions options, UiButtonInteraction interaction, bool dismissible);
    static virtual UiVector2 MeasureInteraction(UiControlContext context, UiTagContent content, UiTagOptions options, bool dismissible)
        => throw new NotSupportedException("This presentation does not support InteractionTag.");
    static virtual float SecondaryWidth(UiTagOptions options)
        => throw new NotSupportedException("This presentation does not support InteractionTag.");
    static virtual void PaintInteraction(UiControlContext context, UiTagContent content, UiRect bounds, UiRect primary, UiRect secondary,
        UiTagOptions options, UiButtonInteraction primaryInteraction, UiButtonInteraction secondaryInteraction, bool dismissible)
        => throw new NotSupportedException("This presentation does not support InteractionTag.");
}

/// <summary>A passive value or one dismiss action, using the common control input and icon pipelines.</summary>
public class UiTag<TDismissBehavior, TPresentation> : UiControlWidget
    where TDismissBehavior : IUiPointerActivationBehavior
    where TPresentation : IUiTagPresentation
{
    private UiTagOptions _options;
    private UiTagContent _content;
    private UiTagMedia _media;
    private Action? _dismissed;
    internal string? ItemId;
    internal bool ItemEnabled = true;
    public UiTagContent Content => _content;
    protected UiTagContent BoundContent => _content with { Media = _media };
    protected bool IsDismissible => _dismissed is not null;
    protected void OnDismissed() => _dismissed?.Invoke();
    public UiTagOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            value.Validate();
            if (_options == value)
                return;
            ThrowIfBuilding();
            _options = value;
            RequestRebuild();
        }
    }
    public UiTag(UiTagContent content, Action? dismissed = null, UiTagOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content.Text);
        if (UiAccessibilityTree.ContainsControl(content.Text, false)
            || content.SecondaryText is { } secondary && (string.IsNullOrWhiteSpace(secondary) || UiAccessibilityTree.ContainsControl(secondary, false)))
            throw new ArgumentException("Tag content must contain single-line text.", nameof(content));
        if (!content.Icon.IsEmpty && !content.Media.IsEmpty)
            throw new ArgumentException("Tag leading content must use either an icon or media.", nameof(content));
        _options = options ?? new();
        _options.Validate();
        _content = content;
        _media = content.Media.Bind();
        _dismissed = dismissed;
    }
    public UiTag(string text, Action? dismissed = null, UiTagOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : this(new UiTagContent(text), dismissed, options, identity) { }
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context)
        => context.LayoutConstraints.Constrain(TPresentation.Measure(context, _content,
            _options.WithInheritedDirection(context.RightToLeft), _dismissed is not null));
    protected override void RenderControl(UiImmediateContext ui)
    {
        if (ui.RenderTag<TPresentation>(ItemId ?? PrimaryInputId(_content.Text), BoundContent, _options, _dismissed is not null,
            ItemEnabled, TDismissBehavior.PointerActivation))
            _dismissed?.Invoke();
    }
    private protected override void ReleaseOwnedResources()
    {
        _dismissed = null;
        _media.Release();
        _media = default;
        _content = default;
        ItemId = null;
        base.ReleaseOwnedResources();
    }
}

/// <summary>A Tag whose dismiss action activates on pointer release.</summary>
public class UiTag<TPresentation> : UiTag<UiReleaseButtonBehavior, TPresentation>
    where TPresentation : IUiTagPresentation
{
    public UiTag(UiTagContent content, Action? dismissed = null, UiTagOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(content, dismissed, options, identity) { }

    public UiTag(string text, Action? dismissed = null, UiTagOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(text, dismissed, options, identity) { }
}

[UiWidgetStyle(typeof(UiTag<>), typeof(UiFluentTagPresentation))]
[UiComponent("Tag", UiComponentExposure.Fluent)]
public partial class UiFluentTag;
