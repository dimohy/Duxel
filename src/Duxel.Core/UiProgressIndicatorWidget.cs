namespace Duxel.Core;

public sealed record UiProgressIndicatorOptions(string? Label = null, string? Description = null,
    float? PercentComplete = null, bool ProgressHidden = false, float BarHeight = 2, float Width = 180,
    bool ReducedMotion = false, string? AccessibleLabel = null, string? ValueText = null)
{
    internal void Validate()
    {
        if (PercentComplete is { } value && !float.IsFinite(value) || !float.IsFinite(BarHeight) || BarHeight <= 0
            || !float.IsFinite(Width) || Width <= 0)
            throw new ArgumentOutOfRangeException(nameof(UiProgressIndicatorOptions));
    }
}

/// <summary>Owns a progress track with an optional visible label, description and accessible status text.</summary>
public class UiProgressIndicatorWidget : UiArrangedChildrenWidget
{
    private UiProgressIndicatorOptions _options;
    private UiText<UiFluentTextPresentation> LabelText => (UiText<UiFluentTextPresentation>)Children[0];
    private IndicatorTrack Track => (IndicatorTrack)Children[1];
    private UiText<UiIndicatorDescriptionPresentation> DescriptionText =>
        (UiText<UiIndicatorDescriptionPresentation>)Children[2];
    public UiProgressIndicatorOptions Options
    {
        get => _options;
        set
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(value);
            value.Validate();
            if (_options == value) return;
            ThrowIfBuilding();
            _options = value;
            LabelText.Text = value.Label ?? string.Empty;
            DescriptionText.Text = value.Description ?? string.Empty;
            Track.Configure(value);
            RequestRebuild();
        }
    }
    public UiProgressIndicatorWidget(UiProgressIndicatorOptions? options = null,
        UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(CreateChildren(options ?? new()), identity)
    {
        _options = options ?? new();
        Track.Owner = this;
    }
    private static UiWidget[] CreateChildren(UiProgressIndicatorOptions options)
    {
        options.Validate();
        return [new UiText<UiFluentTextPresentation>(options.Label ?? string.Empty, identity : UiWidgetIdentity.Instance),
            new IndicatorTrack(options),
            new UiText<UiIndicatorDescriptionPresentation>(
                options.Description ?? string.Empty, identity : UiWidgetIdentity.Instance)];
    }
    protected override bool CanRetainArrangement => true;
    protected override bool ShouldRenderChild(int index) => index switch
    {
        0 => !string.IsNullOrEmpty(_options.Label),
        1 => !_options.ProgressHidden,
        2 => !string.IsNullOrEmpty(_options.Description),
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
    protected override UiVector2 ArrangeChildren(UiControlContext context, Span<UiRect> bounds)
    {
        var width = context.LayoutConstraints.Constrain(new(_options.Width, 0)).X;
        var height = 0f;
        var count = 0;
        for (var i = 0; i < Children.Length; i++)
        {
            if (!ShouldRenderChild(i)) { bounds[i] = default; continue; }
            var child = context.MeasureConstrained(Children[i], new(0, width, 0, float.PositiveInfinity));
            if (count++ > 0) height += 8;
            bounds[i] = new(0, height, width, child.Y);
            height += child.Y;
        }
        return context.LayoutConstraints.Constrain(new(width, height));
    }
    private sealed class IndicatorTrack(UiProgressIndicatorOptions options) : UiControlWidget
    {
        internal UiProgressIndicatorWidget Owner { get; set; } = null!;
        private UiProgressIndicatorOptions _options = options;
        private UiFluentProgressBehavior _behavior;
        internal void Configure(UiProgressIndicatorOptions options)
        {
            _options = options;
            RequestRebuild();
        }
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.ConstrainToLayout(new(_options.Width, _options.BarHeight));
        protected override void RenderControl(UiImmediateContext ui) => ui.RenderProgressIndicatorTrack(
            PrimaryControlId, ref _behavior, _options, Owner.AccessibleName);
        private protected override void ReleaseOwnedResources()
        {
            UiFluentProgressBehavior.Release(ref _behavior);
            base.ReleaseOwnedResources();
        }
    }
}

[UiWidgetStyle(typeof(UiProgressIndicatorWidget))]
[UiComponent("ProgressIndicator", UiComponentExposure.Fluent)]
public partial class UiFluentProgressIndicator;

public sealed partial class UiImmediateContext
{
    internal void RenderProgressIndicatorTrack(string id, ref UiFluentProgressBehavior behavior, UiProgressIndicatorOptions options, string? accessibleName)
    {
        var value = new UiProgressValue(options.PercentComplete, Shape : UiProgressShape.Square, ReducedMotion : options.ReducedMotion);
        RenderProgress<UiFluentProgressBehavior, UiFluentProgressPresentation>(ref behavior, value,
            new(options.Width, options.BarHeight), constrainLayout : true);
        if (_accessibility is not { } tree) return;
        var resolved = ResolveId(id);
        ClaimAccessibilityField(resolved);
        ClaimTooltipAccessibilityTarget(resolved);
        var bounds = IntersectRect(CurrentClipRect, new(_lastItemPos.X, _lastItemPos.Y, _lastItemSize.X, _lastItemSize.Y));
        var name = accessibleName ?? _accessibilityWidget?.AccessibleName ?? (!string.IsNullOrEmpty(options.Label) ? options.Label
            : options.AccessibleLabel ?? _currentField?.Label ?? string.Empty);
        tree.Add(new(resolved, _accessibilityParent, UiAccessibilityRole.ProgressBar, name, bounds, IsControlEnabled,
            bounds.Width <= 0 || bounds.Height <= 0, false, options.ValueText, ReadOnly : true,
            Hint : options.Description ?? _currentField?.Hint, Focusable : false, ValuePattern : false)
        {
            Busy = value.Fraction is null,
            Range = value.Fraction is { } fraction ? new(fraction * 100, 0, 100, 0, 0) : null,
            ItemStatus = options.ValueText,
            Tooltip = TooltipAccessibility(resolved)
        });
    }
}

internal readonly struct UiIndicatorDescriptionPresentation : IUiTextPresentation
{
    public static bool CanRetainMeasurement => true;
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, string text) =>
        context.MeasureTypography<UiRegularTypeface, UiFluentType200>(text, wrap : true);
    public static void Render(UiControlContext context, string text, UiColor? color) =>
        context.TypographyWrapped<UiRegularTypeface, UiFluentType200>(text, color);
}
