namespace Duxel.Core;

public enum UiHeadsUpDisplayKind { Activity, Success, Failure, Custom }

public sealed record UiHeadsUpDisplayContentOptions(UiHeadsUpDisplayKind Kind = UiHeadsUpDisplayKind.Activity,
    string? Label = null, UiMenuIcon Icon = default, bool ReducedMotion = false)
{
    public UiHeadsUpDisplayContentOptions Validate()
    {
        if ((uint)Kind > 3) throw new ArgumentOutOfRangeException(nameof(Kind));
        if (Kind == UiHeadsUpDisplayKind.Custom && Icon.IsEmpty)
            throw new ArgumentException("A custom heads-up display requires an icon.", nameof(Icon));
        return this;
    }
}

/// <summary>The reusable HUD backplate and content; presentation ownership belongs to its host.</summary>
public sealed class UiHeadsUpDisplayContent : UiControlWidget
{
    private UiHeadsUpDisplayContentOptions _options;
    private readonly UiRetainedLayout<Layout> _layout = new();
    private UiActivityIndicatorMotion _motion;
    private Action? _tapped;
    internal bool HostReducedMotion;
    public UiHeadsUpDisplayContent(UiHeadsUpDisplayContentOptions? options = null,
        Action? tapped = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
        => (_options, _tapped) = ((options ?? new()).Validate(), tapped);
    public UiHeadsUpDisplayContentOptions Options
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
            RequestRebuild();
        }
    }
    public Action? Tapped
    {
        get => _tapped;
        set { ThrowIfDisposed(); ThrowIfBuilding(); _tapped = value; RequestRebuild(); }
    }
    internal readonly record struct Layout(UiVector2 Size, float Side, UiLimitedTextLayout Label);
    protected override bool CanRetainMeasurement => true;
    internal override void InvalidateRetainedMeasurement() => _layout.Invalidate();
    private Layout Prepare(UiControlContext context, bool render = false)
    {
        if (_layout.TryGet(context, render, out var cached, context.CanRetainLayoutMeasurement)) return cached;
        var attempt = _layout.Begin(context);
        var maximum = Math.Clamp(MathF.Min(context.LayoutConstraints.MaxWidth, context.LayoutConstraints.MaxHeight), 100, 192);
        const float minimum = 100;
        var side = minimum;
        var label = default(UiLimitedTextLayout);
        if (!string.IsNullOrEmpty(_options.Label))
        {
            using (var font = context.BeginTypography<UiRegularTypeface, UiHudTypography>())
                side = Math.Clamp(context.MeasureTextBlock(_options.Label).Size.X + 24, minimum, maximum);
            label = UiLimitedTextLayout.Measure<UiRegularTypeface, UiHudTypography>(context, _options.Label,
                side - 24, 2, .6f);
            side = Math.Clamp(MathF.Max(side, 20 + 36 + 20 + label.Block.Size.Y + 16), minimum, maximum);
            label = UiLimitedTextLayout.Measure<UiRegularTypeface, UiHudTypography>(context, _options.Label,
                side - 24, 2, .6f);
        }
        var size = context.LayoutConstraints.Constrain(new(side, side));
        var result = new Layout(size, side, label);
        _layout.Commit(context, attempt, result, size, context.CanRetainLayoutMeasurement);
        return result;
    }
    protected override UiVector2 MeasureContent(UiControlContext context) => Prepare(context).Size;
    protected override void RenderControl(UiImmediateContext ui)
    {
        var layout = Prepare(new(ui), render : true);
        ui.RenderHeadsUpDisplayContent(PrimaryControlId, _options, layout, ref _motion, _tapped, HostReducedMotion);
    }
}

internal readonly struct UiHudTypography : IUiTypographyMetrics
{
    public static bool CanRetainMeasurement => true;
    public static float FontSize => 17;
    public static float LineHeight => 22;
}

[UiFluentIcon("checkmark")]
internal readonly partial struct UiHudSuccessIcon;
[UiFluentIcon("dismiss")]
internal readonly partial struct UiHudFailureIcon;

public sealed partial class UiImmediateContext
{
    internal void RenderHeadsUpDisplayContent(string id, UiHeadsUpDisplayContentOptions options,
        UiHeadsUpDisplayContent.Layout layout, ref UiActivityIndicatorMotion motion, Action? tapped, bool hostReducedMotion)
    {
        var context = new UiControlContext(this);
        var origin = AdvanceCursor(layout.Size);
        var allocation = new UiRect(origin.X, origin.Y, layout.Size.X, layout.Size.Y);
        var bounds = new UiRect(origin.X + (layout.Size.X - layout.Side) * .5f,
            origin.Y + (layout.Size.Y - layout.Side) * .5f, layout.Side, layout.Side);
        var interactiveBounds = IntersectRect(bounds, allocation);
        var resolved = ResolveId(id);
        var interaction = default(UiButtonInteraction);
        if (tapped is not null)
        {
            interaction = context.ActivateOnRelease(id, interactiveBounds);
            interaction = ApplyAccessibilityInvoke(resolved, interactiveBounds, interaction);
        }
        var activity = new UiActivityIndicatorOptions(Size : UiActivityIndicatorSize.ExtraLarge,
            IsAnimating : options.Kind == UiHeadsUpDisplayKind.Activity, ReducedMotion : options.ReducedMotion || hostReducedMotion);
        var angle = motion.Sample(activity, GetTime());
        var clip = IntersectRect(CurrentClipRect, interactiveBounds);
        if (clip.Width > 0 && clip.Height > 0)
        {
            using var clipping = context.Clip(clip);
            var foreground = _theme.ForcedColors is not null ? _theme.Text : new UiColor(255, 255, 255);
            var background = _theme.ForcedColors is not null ? _theme.WindowBg : context.FluentColorScheme switch
            {
                UiFluentColorScheme.Light => new UiColor(36, 36, 36),
                UiFluentColorScheme.Dark => new UiColor(61, 61, 61),
                _ => throw new ArgumentOutOfRangeException(nameof(context))
            };
            var indicator = _theme.ForcedColors is not null ? _theme.Text : context.FluentColorScheme switch
            {
                UiFluentColorScheme.Light => new UiColor(143, 143, 143),
                UiFluentColorScheme.Dark => new UiColor(184, 184, 184),
                _ => throw new ArgumentOutOfRangeException(nameof(context))
            };
            context.Frame(bounds, background, _theme.ForcedColors is not null ? foreground : background, 4,
                _theme.ForcedColors is not null ? 1 : 0);
            var hasLabel = !string.IsNullOrEmpty(options.Label);
            var height = 36 + (hasLabel ? 20 + layout.Label.Block.Size.Y : 0);
            var top = bounds.Y + (bounds.Height - height + (hasLabel ? 4 : 0)) * .5f;
            var icon = new UiRect(bounds.X + (bounds.Width - 36) * .5f, top, 36, 36);
            switch (options.Kind)
            {
                case UiHeadsUpDisplayKind.Activity:
                    UiFluentActivityIndicatorPresentation.Paint(context, activity, angle, icon);
                    if (!activity.ReducedMotion) RequestControlAnimation(bounds);
                    break;
                case UiHeadsUpDisplayKind.Success: UiHudSuccessIcon.Paint(context, icon, indicator, interaction); break;
                case UiHeadsUpDisplayKind.Failure: UiHudFailureIcon.Paint(context, icon, indicator, interaction); break;
                case UiHeadsUpDisplayKind.Custom: options.Icon.Paint(context, icon, indicator, interaction); break;
                default: throw new ArgumentOutOfRangeException(nameof(options));
            }
            if (hasLabel)
            {
                using var font = context.BeginTypography<UiRegularTypeface, UiHudTypography>(scale : layout.Label.Scale);
                context.TypographyTextBlock(layout.Label.Block, new(bounds.X + 12, top + 56, bounds.Width - 24,
                    layout.Label.Block.Size.Y), foreground, UiItemHorizontalAlign.Center);
            }
            if (interaction.FocusVisible) context.FocusOutline(bounds, 4);
        }
        ExposeAccessibility(resolved, _accessibilityParent, tapped is null ? UiAccessibilityRole.Image : UiAccessibilityRole.Button,
            string.IsNullOrEmpty(options.Label) ? options.Kind.ToString() : options.Label, interactiveBounds,
            readOnly : tapped is null, focusable : tapped is not null,
            valuePattern : false, literalLabel : true, busy : activity.IsAnimating);
        _lastItemPos = origin;
        _lastItemSize = layout.Size;
        _lastItemId = resolved;
        _lastItemEdited = false;
        if (interaction.Activated) tapped?.Invoke();
    }
}
