namespace Duxel.Core;

public enum UiActivityIndicatorSize { Medium, ExtraSmall, Small, Large, ExtraLarge }
public sealed record UiActivityIndicatorOptions(UiActivityIndicatorSize Size = UiActivityIndicatorSize.Medium,
    bool IsAnimating = false, bool HidesWhenStopped = true, UiColor? Color = null,
    bool ReducedMotion = false, string? AccessibilityLabel = null)
{
    public UiActivityIndicatorOptions Validate()
    {
        if ((uint)Size > 4) throw new ArgumentOutOfRangeException(nameof(Size));
        return this;
    }
}
public interface IUiActivityIndicatorPresentation
{
    static virtual bool CanRetainMeasurement => false;
    static abstract UiVector2 Measure(UiControlContext context, UiActivityIndicatorOptions options);
    static abstract void Paint(UiControlContext context, UiActivityIndicatorOptions options, float angle, UiRect bounds);
}

/// <summary>Owns an indeterminate activity timeline; stopping preserves layout and optionally hides paint.</summary>
public class UiActivityIndicator<TPresentation> : UiControlWidget where TPresentation : IUiActivityIndicatorPresentation
{
    private UiActivityIndicatorOptions _options;
    private UiActivityIndicatorMotion _motion;
    public UiActivityIndicator(UiActivityIndicatorOptions? options = null, UiWidgetIdentity identity = UiWidgetIdentity.Instance) : base(identity)
        => _options = (options ?? new()).Validate();
    public UiActivityIndicatorOptions Options
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
    public bool IsAnimating { get => _options.IsAnimating; set => Options = _options with { IsAnimating = value }; }
    public void Start() => IsAnimating = true;
    public void Stop() => IsAnimating = false;
    protected override bool CanRetainMeasurement => TPresentation.CanRetainMeasurement;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.ConstrainToLayout(TPresentation.Measure(context, _options));
    protected override void RenderControl(UiImmediateContext ui) => ui.RenderActivityIndicator<TPresentation>(PrimaryControlId, ref _motion, _options);
}

internal struct UiActivityIndicatorMotion
{
    private double? _started;
    internal float Sample(UiActivityIndicatorOptions options, double time)
    {
        if (!options.IsAnimating) { _started = null; return 0; }
        _started ??= time;
        return options.ReducedMotion ? 0 : (float)((time - _started.Value) / .75 % 1 * Math.Tau);
    }
}

public readonly struct UiFluentActivityIndicatorPresentation : IUiActivityIndicatorPresentation
{
    public static bool CanRetainMeasurement => true;
    public static float Diameter(UiActivityIndicatorSize size) => size switch
    {
        UiActivityIndicatorSize.ExtraSmall => 12,
        UiActivityIndicatorSize.Small => 16,
        UiActivityIndicatorSize.Medium => 24,
        UiActivityIndicatorSize.Large => 32,
        UiActivityIndicatorSize.ExtraLarge => 36,
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };
    public static float Thickness(UiActivityIndicatorSize size) => size switch
    {
        UiActivityIndicatorSize.ExtraSmall or UiActivityIndicatorSize.Small => 1,
        UiActivityIndicatorSize.Medium => 2,
        UiActivityIndicatorSize.Large => 3,
        UiActivityIndicatorSize.ExtraLarge => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };
    public static UiVector2 Measure(UiControlContext context, UiActivityIndicatorOptions options)
        => new(Diameter(options.Size), Diameter(options.Size));
    public static void Paint(UiControlContext context, UiActivityIndicatorOptions options, float angle, UiRect bounds)
    {
        var color = context.Theme.ForcedColors is not null ? context.Theme.Text : options.Color
            ?? context.FluentColorScheme switch
            {
                UiFluentColorScheme.Light => new UiColor(143, 143, 143),
                UiFluentColorScheme.Dark => new UiColor(184, 184, 184),
                _ => throw new ArgumentOutOfRangeException(nameof(context))
            };
        var radius = Diameter(options.Size) * .5f;
        var stroke = Thickness(options.Size);
        var center = new UiVector2(bounds.X + bounds.Width * .5f, bounds.Y + bounds.Height * .5f);
        var start = angle + MathF.PI * .5f;
        var sweep = MathF.PI * 1.5f;
        context.AnnularSector(center, radius, stroke, start, sweep, color, color);
        var pathRadius = radius - (stroke + 1) * .5f;
        void Cap(float a) => context.Circle(new(center.X + MathF.Sin(a) * pathRadius,
            center.Y - MathF.Cos(a) * pathRadius), stroke * .5f, color);
        Cap(start); Cap(start + sweep);
    }
}

[UiWidgetStyle(typeof(UiActivityIndicator<>), typeof(UiFluentActivityIndicatorPresentation))]
[UiComponent("ActivityIndicator", UiComponentExposure.Fluent)]
public partial class UiFluentActivityIndicator;

public sealed partial class UiImmediateContext
{
    internal void RenderActivityIndicator<TPresentation>(string id, ref UiActivityIndicatorMotion motion, UiActivityIndicatorOptions options)
        where TPresentation : IUiActivityIndicatorPresentation
    {
        var context = new UiControlContext(this);
        var size = ConstrainToLayout(TPresentation.Measure(context, options));
        var origin = AdvanceCursor(size);
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var angle = motion.Sample(options, GetTime());
        var visible = options.IsAnimating || !options.HidesWhenStopped;
        var resolved = ResolveId(id);
        if (visible)
        {
            var clip = IntersectRect(CurrentClipRect, bounds);
            if (clip.Width > 0 && clip.Height > 0)
            {
                if (options.IsAnimating && !options.ReducedMotion) RequestControlAnimation(bounds);
                using var root = context.Clip(bounds);
                TPresentation.Paint(context, options, angle, bounds);
            }
            ExposeAccessibility(resolved, _accessibilityParent, UiAccessibilityRole.Image,
                options.AccessibilityLabel ?? (options.IsAnimating ? "Animating" : "Stopped"), bounds,
                readOnly : true, focusable : false, valuePattern : false, literalLabel : true, busy : options.IsAnimating);
        }
        _lastItemPos = origin;
        _lastItemSize = size;
        _lastItemId = resolved;
        _lastItemEdited = false;
    }
}
